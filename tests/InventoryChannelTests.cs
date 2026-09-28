using EnhancedStorageBackpack;
using System.Text.Json;
int checks = 0;
void Check(bool success, string name) { checks++; if (!success) throw new Exception(name); }
void Reject(string input)
{
    bool rejected = false;
    try { InventoryChannelProbe.Decode(input); }
    catch (InvalidDataException) { rejected = true; }
    catch (JsonException) { rejected = true; }
    Check(rejected, "invalid probe accepted");
}
var request = new InventoryChannelProbe { Session = Guid.NewGuid().ToString("N"),
    Token = Guid.NewGuid().ToString("N"), Nonce = Guid.NewGuid().ToString("N") };
var reply = InventoryChannelProbe.Decode(request.Encode()); reply.Kind = "reply";
Check(reply.IsReplyTo(request), "correlated reply");
reply.Nonce = Guid.NewGuid().ToString("N"); Check(!reply.IsReplyTo(request), "old challenge rejected");
reply.Nonce = request.Nonce; reply.Session = Guid.NewGuid().ToString("N"); Check(!reply.IsReplyTo(request), "old game rejected");
reply.Session = request.Session; reply.Token = Guid.NewGuid().ToString("N"); Check(!reply.IsReplyTo(request), "other connection rejected");
reply.Token = request.Token; reply.Kind = "probe"; Check(!reply.IsReplyTo(request), "request is not confirmation");
reply.Kind = "reply"; reply.Build = "old"; Check(!reply.IsReplyTo(request), "old build rejected");
Reject(reply.Encode());
Reject("null"); Reject("{}"); Reject("[]"); Reject("{"); Reject(new string('a', 1025));
reply.Build = MultiplayerProtocol.Build; reply.Kind = "move"; Reject(reply.Encode());
reply.Kind = "reply"; reply.Nonce = "invalid"; Reject(reply.Encode());
reply.Nonce = request.Nonce; reply.Token = ""; Reject(reply.Encode());
reply.Token = request.Token; reply.Session = ""; Reject(reply.Encode());
Console.WriteLine($"Inventory channel probe: {checks} checks passed; native RPC hooks not simulated.");
