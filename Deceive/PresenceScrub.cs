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
        var valorantNode = presence.Element("games")?.Element("valorant");
        if (valorantNode == null) return;

        ScrubBase64Element(valorantNode, "p", RebuildP);
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