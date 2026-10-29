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
  'x86_64-pc-windows-msvc' => ['win-x64', 'ktav_cabi-windows-amd64.dll'],
  'aarch64-pc-windows-msvc' => ['win-arm64', 'ktav_cabi-windows-arm64.dll']
}
abort 'native target/RID/asset mapping changed' unless settings.length == 6 && settings.to_h { |item| [item.fetch('target'), [item.fetch('rid'), item.fetch('asset')]] } == expected
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
