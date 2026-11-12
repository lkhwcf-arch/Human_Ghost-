using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HumanGhost.Telemetry
{
    public interface ITelemetryTransport
    {
        Task SendAsync(JArray records, CancellationToken cancellation);
    }

    // Windows에서 지원되지 않는 Analytics SDK 대신 Firebase 인증·Firestore REST만 사용합니다.
    public class FirebaseRestTransport : ITelemetryTransport
    {
        private readonly HttpClient _http;
        private readonly string _projectId, _apiKey;
        private string _idToken, _refreshToken, _uid;
        private DateTimeOffset _expires;
        public FirebaseRestTransport(HttpClient http, string projectId, string apiKey)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _projectId = projectId; _apiKey = apiKey;
        }
        private async Task AuthenticateAsync(CancellationToken cancellation)
        {
            if (!string.IsNullOrEmpty(_idToken) && DateTimeOffset.UtcNow < _expires) return;
            bool refresh = !string.IsNullOrEmpty(_refreshToken);
            string url = refresh ? "https://securetoken.googleapis.com/v1/token?key=" + Uri.EscapeDataString(_apiKey)
                : "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + Uri.EscapeDataString(_apiKey);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = refresh
                ? new StringContent("grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(_refreshToken), Encoding.UTF8, "application/x-www-form-urlencoded")
                : new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException("Firebase 인증 실패: HTTP " + (int)response.StatusCode);
            var data = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            _idToken = (string)data[refresh ? "id_token" : "idToken"];
            _refreshToken = (string)data[refresh ? "refresh_token" : "refreshToken"];
            _uid = (string)data[refresh ? "user_id" : "localId"];
            int seconds = int.Parse((string)data[refresh ? "expires_in" : "expiresIn"], CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(_idToken) || string.IsNullOrEmpty(_uid)) throw new InvalidOperationException("Firebase 인증 응답에 사용자·토큰이 없습니다.");
            _expires = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, seconds - 60));
        }
        public async Task SendAsync(JArray records, CancellationToken cancellation)
        {
            foreach (JObject record in records)
            {
                cancellation.ThrowIfCancellationRequested();
                await AuthenticateAsync(cancellation).ConfigureAwait(false);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var copy = (JObject)record.DeepClone();
                    copy["OwnerUid"] = _uid;
                    string id = (string)record["EventId"];
                    if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidOperationException("전송 기록의 이벤트 ID 형식이 잘못됐습니다.");
                    string url = "https://firestore.googleapis.com/v1/projects/" + Uri.EscapeDataString(_projectId)
                        + "/databases/(default)/documents/telemetryEvents?documentId=" + id;
                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idToken);
                    request.Content = new StringContent(new JObject { ["fields"] = EncodeFields(copy) }.ToString(), Encoding.UTF8, "application/json");
                    using var response = await _http.SendAsync(request, cancellation).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) break;
                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        var error = JObject.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        if ((string)error["error"]?["status"] == "ALREADY_EXISTS") break;
                    }
                    if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                    {
                        _expires = DateTimeOffset.MinValue;
                        await AuthenticateAsync(cancellation).ConfigureAwait(false);
                        continue;
                    }
                    throw new HttpRequestException("Firebase 기록 전송 실패: HTTP " + (int)response.StatusCode);
                }
            }
        }
        public static JObject EncodeFields(JObject source)
        {
            var fields = new JObject();
            foreach (var property in source.Properties())
            {
                JToken value = property.Value;
                fields[property.Name] = value.Type switch
                {
                    JTokenType.Object => new JObject { ["mapValue"] = new JObject { ["fields"] = EncodeFields((JObject)value) } },
                    JTokenType.Integer => new JObject { ["integerValue"] = Convert.ToString(((JValue)value).Value, CultureInfo.InvariantCulture) },
                    JTokenType.String => new JObject { ["stringValue"] = value },
                    JTokenType.Boolean => new JObject { ["booleanValue"] = value },
                    JTokenType.Null => new JObject { ["nullValue"] = null },
                    _ => throw new InvalidOperationException("Firestore 전송에 지원하지 않는 값 형식입니다: " + property.Name)
                };
            }
            return fields;
        }
    }
}
