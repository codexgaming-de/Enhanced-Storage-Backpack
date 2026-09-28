using EnhancedStorageBackpack;

int checks = 0;
void Check(bool result, string name) { checks++; if (!result) throw new Exception(name); }
MultiplayerProtocol.Offer Settings() => new()
{
    Protocol = MultiplayerProtocol.Version, Build = MultiplayerProtocol.Build,
    Session = Guid.NewGuid().ToString("N"), BackpackSlots = 64,
    Slots = Enumerable.Repeat(24, 9).ToArray(), Rows = Enumerable.Repeat(4, 9).ToArray()
};
HostSettingsSession.Message Offer(HostSettingsSession state, MultiplayerProtocol.Offer settings, long revision) => new()
{
    Kind = "offer", ClientToken = state.Hello().ClientToken, Settings = settings,
    Session = settings.Session, Revision = revision
};
HostSettingsSession.Message Ready(HostSettingsSession.Message ack) => new()
{ Kind = "ready", ClientToken = ack.ClientToken, Session = ack.Session, Revision = ack.Revision };
var host = Settings();
var client = new HostSettingsSession(22, 11);
var offer = Offer(client, host, 1);
Check(!client.Ready, "not ready before handshake");
Check(client.Receive(33, offer) == null && !client.Ready, "outsider cannot configure");
Check(client.Receive(22, offer) == null && !client.Ready, "client cannot configure itself");
var otherClient = new HostSettingsSession(33, 11);
Check(otherClient.Receive(11, offer) == null && !otherClient.Ready, "other player token rejected");
var ack = client.Receive(11, offer)!;
Check(ack.Kind == "ack" && !client.Ready, "ack does not unlock early");
var ready = Ready(ack); ready.Revision = 2;
client.Receive(11, ready);
Check(!client.Ready, "out of order ready rejected");
ready = Ready(ack); client.Receive(33, ready);
Check(!client.Ready, "outsider cannot confirm host");
client.Receive(11, ready);
Check(client.Ready && client.Effective!.BackpackSlots == 64, "host setting accepted");
host.BackpackSlots = 128;
Check(client.Effective!.BackpackSlots == 64, "defensive snapshot");
var newer = Offer(client, host, 2);
var newerAck = client.Receive(11, newer)!;
Check(!client.Ready, "new revision requires ack");
client.Receive(11, Ready(ack));
Check(!client.Ready, "stale ready rejected");
client.Receive(11, Ready(newerAck));
Check(client.Ready && client.Effective!.BackpackSlots == 128, "live host update");
Check(client.Receive(11, offer) == null && client.Effective!.BackpackSlots == 128, "stale offer rejected");
client.Receive(11, newer);
Check(client.Ready, "duplicate heartbeat does not clear settings");
var conflicting = Offer(client, Settings(), 2); conflicting.Session = host.Session; conflicting.Settings!.Session = host.Session;
Check(client.Receive(11, conflicting) == null && client.Effective!.BackpackSlots == 128, "same revision conflict rejected");
var rejoin = new HostSettingsSession(22, 11);
Check(rejoin.Receive(11, newer) == null && !rejoin.Ready, "old traffic after rejoin rejected");
var otherHost = new HostSettingsSession(22, 44);
Check(otherHost.Receive(11, newer) == null && !otherHost.Ready, "old host rejected");
newer.Build = "0.1.6";
Check(client.Receive(11, newer) == null, "version mismatch rejected");
foreach (string bad in new[] { "not base64", "", new string('x', 16385), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}")) })
{
    bool rejected = false;
    try { HostSettingsSession.Decode(bad); } catch { rejected = true; }
    Check(rejected, "malformed message rejected");
}
var decoded = HostSettingsSession.Decode(HostSettingsSession.Encode(client.Hello()));
Check(decoded.Kind == "hello" && decoded.ClientToken == client.Hello().ClientToken, "wire round trip");
Console.WriteLine($"Host settings handshake: {checks} assertions passed; no Steam/native peers simulated.");
