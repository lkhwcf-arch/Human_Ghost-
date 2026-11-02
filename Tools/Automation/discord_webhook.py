"""디스코드 공지의 전송과 오류 처리를 공통으로 담당합니다."""
import json
import urllib.error
import urllib.parse
import urllib.request


def Send(webhook, embed):
    parsed = urllib.parse.urlsplit(webhook.strip())
    if parsed.scheme != "https" or parsed.hostname != "discord.com" or not parsed.path.startswith("/api/webhooks/"):
        raise ValueError("디스코드 웹훅 비밀값이 없거나 올바르지 않습니다.")
    query = dict(urllib.parse.parse_qsl(parsed.query))
    query["wait"] = "true"
    endpoint = urllib.parse.urlunsplit(parsed._replace(query=urllib.parse.urlencode(query), fragment=""))
    payload = {"allowed_mentions": {"parse": []}, "embeds": [embed]}
    request = urllib.request.Request(
        endpoint, data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"Content-Type": "application/json", "User-Agent": "HumanGhost-Notifier"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as response:
            response.read()
    except urllib.error.HTTPError as error:
        raise RuntimeError(f"디스코드 전송 실패: 응답 코드 {error.code}. 웹훅 설정을 확인해주세요.") from None
    except (urllib.error.URLError, TimeoutError):
        raise RuntimeError("디스코드 연결에 실패했습니다. 실행 기록에서 공지를 다시 시도해주세요.") from None

