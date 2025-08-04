namespace Microsoft.ContentAuthenticy.Bindings
{
    [Serializable]
    public class C2paException(string message) : Exception(message) { }
}