// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
namespace ContentAuthenticity;

/// <summary>
/// Resolves HTTP requests issued by the underlying c2pa library.
/// </summary>
public interface IHttpResolver
{
    HttpResolverResponse Resolve(HttpResolverRequest request);
}

public sealed class HttpResolverRequest
{
    public required Uri Url { get; init; }

    public required string Method { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public byte[]? Body { get; init; }
}

public sealed class HttpResolverResponse
{
    public required int Status { get; init; }

    public byte[] Body { get; init; } = [];
}

/// <summary>
/// Resolves HTTP requests issued by the underlying c2pa library using an <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
public sealed class HttpResolver : IHttpResolver, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly bool ownsClient;

    /// <summary>
    /// Creates a new <see cref="HttpResolver"/> using a default <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public HttpResolver()
        : this(new HttpClient(), ownsClient: true)
    {
    }

    /// <summary>
    /// Creates a new <see cref="HttpResolver"/> using the supplied <see cref="System.Net.Http.HttpClient"/>.
    /// The resolver does not take ownership of the client.
    /// </summary>
    public HttpResolver(HttpClient httpClient)
        : this(httpClient, ownsClient: false)
    {
    }

    private HttpResolver(HttpClient httpClient, bool ownsClient)
    {
        this.httpClient = httpClient;
        this.ownsClient = ownsClient;
    }

    public HttpResolverResponse Resolve(HttpResolverRequest request)
    {
        using var message = BuildRequest(request);
        using var result = httpClient.Send(message, HttpCompletionOption.ResponseContentRead);

        byte[] bodyBytes;
        using (var ms = new MemoryStream())
        {
            result.Content.CopyTo(ms, context: null, cancellationToken: default);
            bodyBytes = ms.ToArray();
        }

        return new HttpResolverResponse
        {
            Status = (int)result.StatusCode,
            Body = bodyBytes
        };
    }

    public void Dispose()
    {
        if (ownsClient)
            httpClient.Dispose();
    }

    private static HttpRequestMessage BuildRequest(HttpResolverRequest request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

        if (request.Body is { Length: > 0 })
            message.Content = new ReadOnlyMemoryContent(request.Body);

        foreach (var (name, value) in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                message.Content ??= new ByteArrayContent([]);
                message.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return message;
    }
}

internal sealed unsafe class C2paHttpResolver : IDisposable
{
    private static readonly char[] HeaderLineSeparators = ['\n'];

    private readonly IHttpResolver resolver;
    private GCHandle handle;
    private Bindings.C2paHttpResolver* nativeResolver;

    public C2paHttpResolver(IHttpResolver resolver)
    {
        this.resolver = resolver;
        handle = GCHandle.Alloc(this);
        nativeResolver = C2paBindings.http_resolver_create((void*)(nint)handle, &OnResolve);
        if (nativeResolver == null)
        {
            handle.Free();
            C2pa.CheckError();
        }
    }

    public static implicit operator Bindings.C2paHttpResolver*(C2paHttpResolver resolver)
    {
        return resolver.nativeResolver;
    }

    public GCHandle DetachHandle()
    {
        var detachedHandle = handle;
        handle = default;
        return detachedHandle;
    }

    public void Dispose()
    {
        if (nativeResolver != null)
        {
            C2paBindings.free(nativeResolver);
            nativeResolver = null;
        }

        if (handle.IsAllocated)
            handle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnResolve(void* context, C2paHttpRequest* request, C2paHttpResponse* response)
    {
        try
        {
            var handle = GCHandle.FromIntPtr((nint)context);
            if (handle.Target is not C2paHttpResolver self)
                return -1;

            var resolverRequest = BuildRequest(request);
            var resolverResponse = self.resolver.Resolve(resolverRequest);

            response->status = resolverResponse.Status;
            response->body_len = (nuint)resolverResponse.Body.Length;
            response->body = AllocateNative(resolverResponse.Body);
            return 0;
        }
        catch (Exception ex)
        {
            C2pa.SetError("Other", ex.Message);
            response->status = 0;
            response->body = null;
            response->body_len = 0;
            return -1;
        }
    }

    private static HttpResolverRequest BuildRequest(C2paHttpRequest* request)
    {
        string url = Utils.FromCString(request->url, freeResource: false);
        string methodStr = Utils.FromCString(request->method, freeResource: false);
        byte[]? body = null;

        if (request->body != null && request->body_len > 0)
        {
            body = new byte[(int)request->body_len];
            Marshal.Copy((nint)request->body, body, 0, body.Length);
        }

        var headers = new Dictionary<string, string>();

        string headersRaw = request->headers != null
            ? Utils.FromCString(request->headers, freeResource: false)
            : string.Empty;
        if (!string.IsNullOrEmpty(headersRaw))
        {
            foreach (var line in headersRaw.Split(HeaderLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                string name = line[..colon].Trim();
                string value = line[(colon + 1)..].Trim();
                if (name.Length == 0)
                    continue;

                headers[name] = value;
            }
        }

        return new HttpResolverRequest
        {
            Url = new Uri(url),
            Method = methodStr,
            Headers = headers,
            Body = body
        };
    }

    private static unsafe byte* AllocateNative(byte[] data)
    {
        if (data.Length == 0)
            return null;

        // Rust will free this with the C `free()`, so use the C runtime allocator.
        byte* ptr = (byte*)NativeMemory.Alloc((nuint)data.Length);
        Marshal.Copy(data, 0, (nint)ptr, data.Length);
        return ptr;
    }
}