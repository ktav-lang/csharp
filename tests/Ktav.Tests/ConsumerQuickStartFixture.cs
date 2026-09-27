// Compiled in the global namespace to match consumer name resolution.
// README imports begin
using Ktav;
// README imports end

internal static class ConsumerQuickStartFixture
{
    internal static (string Service, long Port, double Ratio, bool Tls, string Host, long Timeout) Run()
    {
        // README body begin
        const string src = """
                           service: web
                           port: 8080
                           ratio: 0.75
                           tls: true
                           tags: [
                               prod
                               eu-west-1
                           ]
                           db.host: primary.internal
                           db.timeout: 30
                           """;

        var top = (KtavObject)global::Ktav.Ktav.Loads(src);

        string  service = ((KtavString)  top.TryGet("service")!).Value;
        long    port    = ((KtavInteger) top.TryGet("port")!).ToInt64();
        double  ratio   = ((KtavFloat)   top.TryGet("ratio")!).ToDouble();
        bool    tls     = ((KtavBool)    top.TryGet("tls")!).Value;

        var db = (KtavObject) top.TryGet("db")!;
        string dbHost   = ((KtavString)  db.TryGet("host")!).Value;
        long   dbTimeout = ((KtavInteger) db.TryGet("timeout")!).ToInt64();
        // README body end
        return (service, port, ratio, tls, dbHost, dbTimeout);
    }
}
