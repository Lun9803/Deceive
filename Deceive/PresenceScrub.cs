using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Deceive;

static class PresenceScrub
{
    public static void ScrubValorantPresence(XElement presence)
    {
        var gamesNode = presence.Element("games");
        var valorantNode = gamesNode?.Element("valorant");
        if (valorantNode == null) return;

        // VALORANT needs an active product status to render the player card in a
        // party. The separate party/activity payload is not needed for that.
        valorantNode.Element("pd")?.Remove();
        // Away is the least-visible tested status that still renders the lobby card.
        valorantNode.SetElementValue("st", "away");
        ScrubBase64Element(valorantNode, "p", RebuildP);

        // Riot Client selects the newer product presence, masking VALORANT as
        // Legends of Runeterra away while VALORANT still reads its lobby data.
        var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        gamesNode!.Add(
            new XElement("bacon",
                new XElement("st", "away"),
                new XElement("s.t", timestamp),
                new XElement("s.p", "bacon"),
                new XElement("pty")));
        // Do NOT remove valorantNode anymore.
    }

    private static void ScrubBase64Element(XElement valorantNode, string elementName, Func<JsonObject, JsonObject> rebuild)
    {
        var el = valorantNode.Element(elementName);
        if (el == null || string.IsNullOrEmpty(el.Value)) return;

        byte[] raw;
        try { raw = Convert.FromBase64String(el.Value); }
        catch { return; }

        JsonObject? obj;
        try { obj = JsonNode.Parse(Encoding.UTF8.GetString(raw))?.AsObject(); }
        catch { return; }
        if (obj == null) return;

        var rebuilt = rebuild(obj);
        var json = rebuilt.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        el.Value = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    // Keep ONLY playerPresenceData (card/title/level/etc). Everything else
    // becomes a minimal, schema-valid, non-revealing default — never deleted,
    // just blanked, so the client's deserializer doesn't choke on a missing key.
    private static JsonObject RebuildP(JsonObject original)
    {
        var playerPresenceDataNode = original["playerPresenceData"];
        var playerPresenceData = playerPresenceDataNode != null
            ? JsonNode.Parse(playerPresenceDataNode.ToJsonString())!.AsObject()
            : new JsonObject();

        return new JsonObject
        {
            ["isValid"] = true,
            ["playerPresenceData"] = playerPresenceData, 
        };
    }
}
