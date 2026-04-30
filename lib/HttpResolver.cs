// Copyright (c) All Contributors. All Rights Reserved. Licensed under the MIT License (MIT). See License.md in the repository root for more information.
using System.Net.Http;

namespace ContentAuthenticity;

/// <summary>
/// Managed wrapper around the native <see cref="C2paHttpResolver"/>. Routes
/// HTTP requests issued by the underlying c2pa library (remote manifest
/// fetches, OCSP, timestamp, etc.) through an <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
public sealed class HttpResolver : IDisposable
{
    private static readonly char[] HeaderLineSeparators = ['\n'];

    private readonly HttpClient httpClient;
    private readonly bool ownsClient;
    private readonly GCHandle handle;
    private unsafe readonly C2paHttpResolver* resolver;
    private bool consumed;

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
        ArgumentNullException.ThrowIfNull(httpClient);
    }

    private HttpResolver(HttpClient httpClient, bool ownsClient)
    {
        this.httpClient = httpClient;
        this.ownsClient = ownsClient;

        handle = GCHandle.Alloc(this);
        unsafe
        {
            resolver = C2paBindings.http_resolver_create((void*)(nint)handle, &OnResolve);
            if (resolver == null)
            {
                handle.Free();
                C2pa.CheckError();
            }
        }
    }

    public static unsafe implicit operator C2paHttpResolver*(HttpResolver resolver)
    {
        return resolver.resolver;
    }

    /// <summary>
    /// Marks the native resolver as having been consumed by another native
    /// call (e.g. <c>c2pa_context_builder_set_http_resolver</c>). After this,
    /// <see cref="Dispose"/> will not call <c>c2pa_free</c>.
    /// </summary>
    internal void MarkConsumed()
    {
        consumed = true;
    }

    public void Dispose()
    {
        unsafe
        {
            if (!consumed)
            {
                C2paBindings.free(resolver);
            }
        }

        if (handle.IsAllocated)
            handle.Free();

        if (ownsClient)
            httpClient.Dispose();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe int OnResolve(void* context, C2paHttpRequest* request, C2paHttpResponse* response)
    {
        Console.WriteLine("Resolvin http request...");
        try
        {
            var handle = GCHandle.FromIntPtr((nint)context);
            if (handle.Target is not HttpResolver self)
                return -1;

            using var message = BuildRequest(request);
            using var result = self.httpClient.Send(message, HttpCompletionOption.ResponseContentRead);

            byte[] bodyBytes;
            using (var ms = new MemoryStream())
            {
                result.Content.CopyTo(ms, context: null, cancellationToken: default);
                bodyBytes = ms.ToArray();
            }

            response->status = (int)result.StatusCode;
            response->body_len = (nuint)bodyBytes.Length;
            response->body = AllocateNative(bodyBytes);
            return 0;
        }
        catch (Exception ex)
        {
            SetLastError(ex.Message);
            response->status = 0;
            response->body = null;
            response->body_len = 0;
            return -1;
        }
    }

    private static unsafe HttpRequestMessage BuildRequest(C2paHttpRequest* request)
    {
        string url = Utils.FromCString(request->url, freeResource: false);
        string methodStr = Utils.FromCString(request->method, freeResource: false);
        var method = new HttpMethod(methodStr);
        var message = new HttpRequestMessage(method, url);

        if (request->body != null && request->body_len > 0)
        {
            var body = new byte[(int)request->body_len];
            Marshal.Copy((nint)request->body, body, 0, body.Length);
            message.Content = new ReadOnlyMemoryContent(body);
        }

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

                if (!message.Headers.TryAddWithoutValidation(name, value))
                {
                    message.Content ??= new ByteArrayContent([]);
                    message.Content.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        return message;
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

    private static unsafe void SetLastError(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message + "\0");
        fixed (byte* p = bytes)
        {
            C2paBindings.error_set_last((sbyte*)p);
        }
    }
}