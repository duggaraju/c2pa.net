namespace Microsoft.ContentAuthenticity.Bindings
{
    [Serializable]
    public class C2paException(string type, string message) : Exception(message)
    {
        public readonly string Type = type;
    }
}