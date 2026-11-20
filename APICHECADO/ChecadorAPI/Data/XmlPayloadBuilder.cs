using System.Xml.Linq;

namespace ChecadorAPI.Data;

public static class XmlPayloadBuilder
{
    public static string BuildXml(string accion, Dictionary<string, object?> parametros)
    {
        var root = new XElement("root",
            new XElement("accion", accion)
        );

        foreach (var (key, value) in parametros)
        {
            if (value != null)
            {
                root.Add(new XElement(key, value.ToString()));
            }
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }
}
