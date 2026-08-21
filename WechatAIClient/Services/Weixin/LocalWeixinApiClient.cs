using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WechatAIClient.Models;

namespace WechatAIClient.Services.Weixin;

public interface ILocalWeixinApiClient
{
    string BaseUrl { get; }
    Task<LocalApiResult<CheckLoginResponse>> CheckLoginAsync(CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> WechatInitAsync(CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> InitRoomsAsync(CancellationToken cancellationToken = default);
    Task<LocalApiResult<ContactListResponse>> GetContactList2Async(CancellationToken cancellationToken = default);
    Task<LocalApiResult<ChatroomListResponse>> GetChatroomListAsync(CancellationToken cancellationToken = default);
    Task<LocalApiResult<RoomMembersResponse>> GetRoomMembersAsync(string roomId, CancellationToken cancellationToken = default);
    Task<LocalApiResult<MemberNickResponse>> GetMemberNickAsync(string wxid, string roomId, CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> SendTextAsync(string wxid, string msg, CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> SendImageAsync(string wxid, string filepath, CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> SendFileAsync(string wxid, string filepath, CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> SendAtTextAsync(SendAtTextRequest request, CancellationToken cancellationToken = default);
    Task<LocalApiResult<SimpleCodeResponse>> SendQuoteAsync(SendQuoteRequest request, CancellationToken cancellationToken = default);
    Task<LocalApiResult<JsonElement>> DownloadImgAsync(object request, CancellationToken cancellationToken = default);
    Task<LocalApiResult<JsonElement>> DownloadFileAsync(object request, CancellationToken cancellationToken = default);
    Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalWeixinApiClient : ILocalWeixinApiClient
{
    public const string HttpClientName = "WeixinLocalApi";
    public const string DefaultBaseUrl = "http://127.0.0.1:19088";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LocalWeixinApiClient> _logger;
    private string _baseUrl = DefaultBaseUrl;
    private bool _baseUrlSet;

    public LocalWeixinApiClient(IHttpClientFactory httpClientFactory, ILogger<LocalWeixinApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Base URL for this client instance. Settable once (per-session clients).</summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set
        {
            if (_baseUrlSet)
            {
                return;
            }

            _baseUrl = string.IsNullOrWhiteSpace(value) ? DefaultBaseUrl : value.TrimEnd('/');
            _baseUrlSet = true;
        }
    }

    public async Task<bool> IsApiReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/check_login");
            req.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            using var resp = await client.SendAsync(req, cancellationToken);
            // Got an HTTP response — reachable regardless of Success / logged-in.
            return (int)resp.StatusCode is >= 100 and < 600;
        }
        catch
        {
            return false;
        }
    }

    public Task<LocalApiResult<CheckLoginResponse>> CheckLoginAsync(CancellationToken cancellationToken = default)
        => PostAsync<CheckLoginResponse>("/api/check_login", new { }, IsLoginSuccess, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> WechatInitAsync(CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/wechat_init", new { }, IsGenericOk, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> InitRoomsAsync(CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/init_rooms", new { }, IsGenericOk, cancellationToken);

    public Task<LocalApiResult<ContactListResponse>> GetContactList2Async(CancellationToken cancellationToken = default)
        => PostAsync<ContactListResponse>("/api/get_contact_list2", new { },
            (_, body) => body?.FriendList is not null || body?.FriendCount >= 0, cancellationToken);

    public Task<LocalApiResult<ChatroomListResponse>> GetChatroomListAsync(CancellationToken cancellationToken = default)
        => PostAsync<ChatroomListResponse>("/api/get_chatroom_list", new { },
            (resp, body) => body?.Data is not null || body?.Code is 0 or 1, cancellationToken);

    public Task<LocalApiResult<RoomMembersResponse>> GetRoomMembersAsync(string roomId, CancellationToken cancellationToken = default)
        => PostAsync<RoomMembersResponse>("/api/get_room_members", new RoomMembersRequest { RoomId = roomId },
            (_, body) => body?.NewChatroomData?.ChatRoomMember is not null || body is not null, cancellationToken);

    public Task<LocalApiResult<MemberNickResponse>> GetMemberNickAsync(string wxid, string roomId, CancellationToken cancellationToken = default)
        => PostAsync<MemberNickResponse>("/api/get_member_nick", new MemberNickRequest { Wxid = wxid, RoomId = roomId },
            (_, body) => body?.Data is not null || body?.ErrCode is 0 or 1, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> SendTextAsync(string wxid, string msg, CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/send_text_msg", new SendTextRequest { Wxid = wxid, Msg = msg }, IsSendSuccess, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> SendImageAsync(string wxid, string filepath, CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/send_image_msg", new SendImageRequest { Wxid = wxid, Filepath = filepath }, IsSendSuccess, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> SendFileAsync(string wxid, string filepath, CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/send_file_msg", new SendFileRequest { Wxid = wxid, Filepath = filepath }, IsSendSuccess, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> SendAtTextAsync(SendAtTextRequest request, CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/send_at_text", request, IsSendSuccess, cancellationToken);

    public Task<LocalApiResult<SimpleCodeResponse>> SendQuoteAsync(SendQuoteRequest request, CancellationToken cancellationToken = default)
        => PostAsync<SimpleCodeResponse>("/api/send_quote", request, IsQuoteSuccess, cancellationToken);

    public Task<LocalApiResult<JsonElement>> DownloadImgAsync(object request, CancellationToken cancellationToken = default)
        => PostAsync<JsonElement>("/api/download_img", request, (_, _) => true, cancellationToken);

    public Task<LocalApiResult<JsonElement>> DownloadFileAsync(object request, CancellationToken cancellationToken = default)
        => PostAsync<JsonElement>("/api/download_file", request, (_, _) => true, cancellationToken);

    private async Task<LocalApiResult<T>> PostAsync<T>(
        string path,
        object body,
        Func<HttpResponseMessage, T?, bool> successPredicate,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(45);
        var url = $"{BaseUrl}{path}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Fail<T>(status, null, "空响应", "EmptyBody");
            }

            T? data;
            try
            {
                data = JsonSerializer.Deserialize<T>(raw, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Weixin API invalid JSON path={Path} status={Status}", path, status);
                return Fail<T>(status, null, "非法 JSON", "InvalidJson");
            }

            var ok = response.IsSuccessStatusCode && successPredicate(response, data);
            var (code, message) = ExtractCodeMessage(data);
            _logger.LogInformation(
                "Weixin API {Path} http={Status} success={Ok} apiCode={Code}",
                path, status, ok, code);

            return new LocalApiResult<T>
            {
                Success = ok,
                HttpStatus = status,
                ApiCode = code,
                ApiMessage = message,
                Data = data,
                ErrorMessage = ok ? null : (message ?? $"接口失败 HTTP {status}")
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(0, null, "请求超时", nameof(TimeoutException));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Weixin API unreachable path={Path}", path);
            return Fail<T>(0, null, "Hook API 未连接", "HookApiOffline");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weixin API unexpected path={Path}", path);
            return Fail<T>(0, null, "接口异常", ex.GetType().Name);
        }
    }

    private static LocalApiResult<T> Fail<T>(int status, int? code, string message, string? exType)
        => new()
        {
            Success = false,
            HttpStatus = status,
            ApiCode = code,
            ApiMessage = message,
            ErrorMessage = message,
            ExceptionType = exType
        };

    // send_* typically code=1
    private static bool IsSendSuccess(HttpResponseMessage _, SimpleCodeResponse? body)
        => body is not null && (body.Code == 1 || string.Equals(body.Msg, "success", StringComparison.OrdinalIgnoreCase));

    // quote often errCode=1
    private static bool IsQuoteSuccess(HttpResponseMessage _, SimpleCodeResponse? body)
        => body is not null && (body.ErrCode == 1 || body.Code == 1 ||
            (body.ErrMsg?.Contains("成功", StringComparison.Ordinal) ?? false));

    private static bool IsGenericOk(HttpResponseMessage _, SimpleCodeResponse? body)
        => body is null || body.Code is null or 0 or 1 || body.ErrCode is null or 0 or 1;

    private static bool IsLoginSuccess(HttpResponseMessage _, CheckLoginResponse? body)
    {
        if (body is null)
        {
            return false;
        }

        // Prefer explicit logged-in signals; tolerate multiple schemas.
        if (!string.IsNullOrWhiteSpace(body.AccountWxid) ||
            !string.IsNullOrWhiteSpace(body.Wxid) ||
            !string.IsNullOrWhiteSpace(body.Data?.Wxid))
        {
            return true;
        }

        if (body.Code == 1 || body.ErrCode == 1)
        {
            return true;
        }

        var msg = body.Msg ?? body.ErrMsg ?? "";
        if (msg.Contains("未登录", StringComparison.Ordinal) ||
            msg.Contains("not login", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return body.Code == 0; // some APIs use 0 as ok with empty body while logged in
    }

    private static (int? Code, string? Message) ExtractCodeMessage<T>(T? data)
    {
        return data switch
        {
            SimpleCodeResponse s => (s.Code ?? s.ErrCode, s.Msg ?? s.ErrMsg),
            CheckLoginResponse c => (c.Code ?? c.ErrCode, c.Msg ?? c.ErrMsg),
            ChatroomListResponse g => (g.Code, g.Msg),
            MemberNickResponse m => (m.ErrCode, m.ErrMsg),
            _ => (null, null)
        };
    }
}
