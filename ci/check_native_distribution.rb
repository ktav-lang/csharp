require 'yaml'

path = './.github/workflows/native-distribution.yml'
workflow = YAML.load_file(path)
triggers = workflow['on'] || workflow[true]
abort 'missing native workflow contract' unless triggers.fetch('workflow_call').fetch('inputs').fetch('source-ref').fetch('required')
job = workflow.fetch('jobs').fetch('build-native')
settings = job.fetch('strategy').fetch('matrix').fetch('settings')
expected = {
  'x86_64-unknown-linux-gnu' => ['linux-x64', 'libktav_cabi-linux-amd64.so'],
  'aarch64-unknown-linux-gnu' => ['linux-arm64', 'libktav_cabi-linux-arm64.so'],
  'x86_64-apple-darwin' => ['osx-x64', 'libktav_cabi-darwin-amd64.dylib'],
  'aarch64-apple-darwin' => ['osx-arm64', 'libktav_cabi-darwin-arm64.dylib'],
  'x86_64-pc-windows-gnullvm' => ['win-x64', 'ktav_cabi-windows-amd64.dll'],
  'aarch64-pc-windows-gnullvm' => ['win-arm64', 'ktav_cabi-windows-arm64.dll']
}
gnu_path = './.github/workflows/windows-gnu-cabi-candidate.yml'
gnu_call = workflow.fetch('jobs').fetch('windows-gnu')
abort 'wrong GNU producer caller' unless gnu_call.fetch('uses') == gnu_path
abort 'GNU source is not forwarded' unless gnu_call.fetch('with').fetch('source-ref') == '${{ inputs.source-ref }}'
gnu = YAML.load_file(gnu_path)
gnu_triggers = gnu['on'] || gnu[true]
abort 'missing GNU exact-source contract' unless gnu_triggers.fetch('workflow_call').fetch('inputs').fetch('source-ref').fetch('required')
abort 'duplicate GNU PR producer' if gnu_triggers.key?('pull_request')
gnu_jobs = gnu.fetch('jobs')
gnu_targets = gnu_jobs.fetch('cabi-gnullvm').fetch('strategy').fetch('matrix').fetch('include').map { |item| item.fetch('target') }
abort 'wrong GNU target matrix' unless gnu_targets.sort == %w[x86_64-pc-windows-gnullvm aarch64-pc-windows-gnullvm].sort
consumers = gnu_jobs.fetch('dotnet-consumers')
abort 'missing GNU producer dependency' unless consumers.fetch('needs') == 'cabi-gnullvm'
actual = consumers.fetch('strategy').fetch('matrix').fetch('include').map { |item| [item.fetch('os'), item.fetch('arch'), item.fetch('target')] }
required = [['windows-latest', 'x64', 'x86_64-pc-windows-gnullvm'], ['windows-11-arm', 'arm64', 'aarch64-pc-windows-gnullvm']]
abort 'incomplete genuine dotnet consumer matrix' unless actual.sort == required.sort
staging = gnu_jobs.fetch('stage-release')
abort 'missing complete GNU consumer gate' unless staging.fetch('needs').sort == %w[cabi-gnullvm dotnet-consumers].sort
windows = staging.fetch('strategy').fetch('matrix').fetch('settings')
abort 'native target/RID/asset mapping changed' unless settings.length == 4 && windows.length == 2 && (settings + windows).to_h { |item| [item.fetch('target'), [item.fetch('rid'), item.fetch('asset')]] } == expected
steps = staging.fetch('steps')
abort 'missing GNU staging command' unless steps.any? { |item| item['run'].to_s.include?('python -I scripts/stage_csharp_gnu_release.py') && !item.key?('if') && !item['continue-on-error'] }
outputs = steps.select { |item| item['uses'].to_s.start_with?('actions/upload-artifact@') }
abort 'wrong GNU raw/RID artifact roles' unless outputs.length == 2 && outputs.map { |item| item.fetch('with').fetch('name') }.sort == ['release-asset-${{ matrix.settings.rid }}', 'runtimes-${{ matrix.settings.rid }}'].sort && outputs.all? { |item| !item.key?('if') && !item['continue-on-error'] && item.fetch('with').fetch('if-no-files-found') == 'error' }
raw = outputs.find { |item| item.fetch('with').fetch('name').start_with?('release-asset-') }
abort 'missing GNU raw notice sidecars' unless raw.fetch('with').fetch('path').include?('.NOTICE.json') && raw.fetch('with').fetch('path').include?('.NOTICE.manifest.json')
rid = outputs.find { |item| item.fetch('with').fetch('name').start_with?('runtimes-') }
abort 'wrong GNU RID layout' unless rid.fetch('with').fetch('path') == '${{ runner.temp }}/csharp-gnu-release/runtimes/'
stager = File.read('scripts/stage_csharp_gnu_release.py')
%w[ktav_cabi-windows-amd64.dll ktav_cabi-windows-arm64.dll .NOTICE.json .NOTICE.manifest.json native/ktav_cabi.dll NOTICE.manifest.json check_embedded_notices.py source_ref target].each do |role|
  abort "missing GNU delivery role #{role}" unless stager.include?(role)
end
caller = YAML.load_file('.github/workflows/native-distribution-check.yml').fetch('jobs').fetch('native-distribution')
abort 'wrong native CI caller' unless caller.fetch('uses') == path
abort 'native CI source is not published C# 0.8.1' unless caller.fetch('with').fetch('source-ref') == '85ae0b1a1fbb67460ac501099ef0739797f786d6'
release = YAML.load_file('.github/workflows/release.yml').fetch('jobs')
producer = release.fetch('build-native')
abort 'production build differs from CI' unless producer.fetch('uses') == path && producer.fetch('with').fetch('source-ref') == '${{ github.sha }}'
abort 'NuGet pack lacks native gate' unless Array(release.fetch('pack').fetch('needs')).include?('build-native')
abort 'publication lacks package gate' unless Array(release.fetch('release').fetch('needs')).include?('pack')
downloads = release.fetch('release').fetch('steps').select { |step| step['uses'].to_s.start_with?('actions/download-artifact@') }
abort 'unsafe publication artifact selector' unless downloads.map { |step| step.fetch('with').slice('name', 'pattern') } == [{'pattern' => 'release-asset-*'}, {'name' => 'nupkg'}]
pack_path = './.github/workflows/nuget-distribution.yml'
producer = release.fetch('pack')
abort 'production NuGet pack differs from CI' unless producer.fetch('uses') == pack_path && producer.fetch('with').fetch('source-ref') == '${{ github.sha }}'
pack_caller = YAML.load_file('.github/workflows/native-distribution-check.yml').fetch('jobs').fetch('pack')
abort 'PR NuGet source differs from native source' unless pack_caller.fetch('uses') == pack_path && pack_caller.fetch('with').fetch('source-ref') == caller.fetch('with').fetch('source-ref') && Array(pack_caller.fetch('needs')).include?('native-distribution')
pack_workflow = YAML.load_file(pack_path)
pack_contract = pack_workflow['on'] || pack_workflow[true]
abort 'missing NuGet exact-source contract' unless pack_contract.fetch('workflow_call').fetch('inputs').fetch('source-ref').fetch('required')
pack_steps = pack_workflow.fetch('jobs').fetch('pack').fetch('steps')
pack_downloads = pack_steps.select { |step| step['uses'].to_s.start_with?('actions/download-artifact@') }
abort 'unsafe NuGet native artifact selector' unless pack_downloads.map { |step| step.fetch('with').slice('name', 'pattern') } == [{'pattern' => 'runtimes-*'}]
['Stage reviewed licensing overlay without API or framework changes', 'Verify NuGet container and overlay guards', 'Lay out exact RID native and notice files', 'Pack both original managed frameworks', 'Verify actual native-bearing NuGet and source identity'].each do |name|
  step = pack_steps.find { |item| item['name'] == name }
  abort "missing mandatory NuGet gate: #{name}" unless step && !step.key?('if') && !step['continue-on-error']
end
nupkg = pack_steps.find { |step| step['uses'].to_s.start_with?('actions/upload-artifact@') && step.fetch('with').fetch('name') == 'nupkg' }
abort 'unsafe NuGet distribution artifact' unless nupkg && nupkg.fetch('with').fetch('path') == 'artifacts/*.nupkg'
steps = job.fetch('steps')
['Prepare locked native notice resource', 'Verify embedded resource and write distribution sidecars', 'Run dotnet tests on matching target architecture', 'Stage exact RID native and notice payloads'].each do |name|
  step = steps.find { |item| item['name'] == name }
  abort "missing mandatory native gate: #{name}" unless step && !step.key?('if') && !step['continue-on-error']
end
uploads = steps.select { |step| step['uses'].to_s.start_with?('actions/upload-artifact@') }
raw = uploads.find { |step| step.fetch('with').fetch('name').start_with?('release-asset-') }
rid = uploads.find { |step| step.fetch('with').fetch('name').start_with?('runtimes-') }
abort 'missing raw notice sidecars' unless raw && raw.fetch('with').fetch('path').include?('.NOTICE.json') && raw.fetch('with').fetch('path').include?('.NOTICE.manifest.json')
abort 'missing RID notice payloads' unless rid && rid.fetch('with').fetch('path') == 'stage/runtimes/'
puts 'C# native source, RID notice layout, runtime and publication contracts verified'
