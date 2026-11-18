using System.Collections.Generic;
using UnityEngine;
using HumanGhost.Data.Definitions;
using HumanGhost.Data.Storage;

namespace HumanGhost.Gameplay.Adapters
{
    // Talk(K11·K15): 대사를 보여 주고, 다시 말을 걸면 마지막 줄만 보여 줍니다. 저장하지 않습니다.
    // 2026-11-17 이름표: NPC 본체 그림 위끝 기준으로 이름표를 두고, 공용 말걸기 안내를 이름표 위에 붙이도록 위치를 알려 줍니다.
    public class NpcTalk : InteractableBehaviour, IInteractionPromptAnchor
    {
        [SerializeField, InspectorName("말하는 사람")] private string _speaker = "";
        [SerializeField, InspectorName("대사 Language ID")] private int[] _lineIds = new int[0];
        [SerializeField, InspectorName("임시 대사(시나리오 공급 전)")] private string[] _temporaryLines = new string[0];
        [SerializeField, InspectorName("대화 뒤 임시 목표 안내")] private string _objective = "";
        private bool _talked;
        [SerializeField, InspectorName("Npc 테이블 ID (0이면 기존 임시 연결)")] private int _npcId;
        private Npc _definition;
        private DialogueRunner _dialogues;
        private TextMesh _nameLabel, _nameShadow;
        private SubscriptionScope _dialogueScope;
        private NpcNameLayout _layout;
        // 본체 그림 위끝(정수)입니다. 연결 때 한 번 재서 애니메이션 프레임마다 이름표가 흔들리지 않게 합니다.
        private long _bodyTop, _bodyCenterX;
        private bool _hasBody, _hasPromptBase;
        private long _promptBaseX, _promptBaseY;
        // 이름표 기준 글자 크기입니다(기존 조정값). 배율은 조립자가 분모 10000 정수로 공급합니다.
        private const float NameCharacterSize = 0.055f, NameFontCharacterSize = 0.027f;
        private const int NameFontSize = 96;
        public int NpcId => _npcId;
        public void SetDefinitionId(int id) => _npcId = id;

        public void Configure(int npcId, IReadOnlyDataCatalog data, DialogueRunner dialogues, TextMesh nameTemplate = null, Font nameFont = null)
            => Configure(npcId, data, dialogues, nameTemplate, nameFont, NpcNameLayout.Default);

        public void Configure(int npcId, IReadOnlyDataCatalog data, DialogueRunner dialogues, TextMesh nameTemplate, Font nameFont, NpcNameLayout layout)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            // 재연결: 이전 구독·소유 대화를 먼저 정리합니다.
            ReleaseDialogue();
            _npcId = npcId; _definition = data.GetTable<Npc>().Get(npcId);
            _dialogues = dialogues ?? throw new System.ArgumentNullException(nameof(dialogues));
            _layout = layout;
            // 생성과 갱신을 분리합니다. 같은 객체를 다시 연결해도 문구·글꼴·크기·활성·배치를 새로 반영합니다.
            if (nameTemplate != null && _nameLabel == null) CreateNameLabel(nameTemplate);
            ApplyName(nameFont);
            NotifyChanged();
            ConnectDialogueEvents();
        }

        private void CreateNameLabel(TextMesh template)
        {
            _nameLabel = Instantiate(template, transform);
            _nameLabel.name = "NpcName";
            _nameLabel.gameObject.SetActive(false);
            // 배경에 묻히지 않도록 같은 글자를 어둡게 한 장 뒤에 둡니다(TextMesh는 UGUI 외곽선 컴포넌트를 쓸 수 없음).
            // 이름표의 형제로 두어 틀 프리팹의 Scale이 두 번 곱해지지 않게 합니다.
            _nameShadow = Instantiate(template, transform);
            _nameShadow.name = "NpcNameShadow";
            _nameShadow.color = new Color(0f, 0f, 0f, .85f);
            _nameShadow.gameObject.SetActive(false);
        }

        // 문구·글꼴·크기·활성 상태를 반영하고 본체 위에 배치합니다.
        private void ApplyName(Font nameFont)
        {
            if (_nameLabel == null || _definition == null) return;
            // 부모 Scale과 무관하게 같은 월드 크기를 유지하도록 부모의 월드 Scale로 나눕니다.
            float parentScale = Mathf.Max(Mathf.Abs(transform.lossyScale.y), .0001f);
            float baseSize = nameFont != null ? NameFontCharacterSize : NameCharacterSize;
            float size = baseSize * _layout.NameScaleRatio / NpcNameLayout.RatioScale / parentScale;
            foreach (TextMesh mesh in new[] { _nameLabel, _nameShadow })
            {
                if (nameFont != null)
                {
                    mesh.font = nameFont; mesh.fontSize = NameFontSize;
                    mesh.GetComponent<MeshRenderer>().sharedMaterial = nameFont.material;
                }
                mesh.characterSize = size;
            }
            _nameShadow.GetComponent<MeshRenderer>().sortingOrder = _nameLabel.GetComponent<MeshRenderer>().sortingOrder - 1;
            MeasureBody();
            RefreshNameText();
        }

        // 본체 = 이 NPC 아래의 그림 렌더러 전체(이름표·그림자 제외). 리깅 NPC처럼 여러 장이면 묶은 범위를 씁니다.
        // [설계 문서와 다른 점] 문서는 본체가 모호하면 오류로 보았으나, 여러 부위 그림은 정상 구성이라 전체 범위를 사용합니다.
        // 본체가 없으면(데이터 없는 임시 NPC) 이름표를 숨기고 안내는 기존 배치를 씁니다.
        private void MeasureBody()
        {
            _hasBody = false;
            Bounds bounds = default;
            foreach (SpriteRenderer body in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (_nameLabel != null && body.transform.IsChildOf(_nameLabel.transform)) continue;
                if (body.sprite == null) continue;
                if (!_hasBody) { bounds = body.bounds; _hasBody = true; } else bounds.Encapsulate(body.bounds);
            }
            if (!_hasBody) return;
            // Unity 경계: 그림 범위는 읽는 즉시 정수로 바꿉니다.
            _bodyTop = ToUnits(bounds.max.y); _bodyCenterX = ToUnits(bounds.center.x);
        }

        // 문구를 바꾸고 이름표 아랫변을 본체 위끝 + 이름 간격에 맞춘 뒤, 안내 기준(이름표 윗변 + 안내 간격)을 갱신합니다.
        private void RefreshNameText()
        {
            _hasPromptBase = false;
            if (_nameLabel == null || _definition == null) return;
            string text = _dialogues.ReadText(_definition.NameTextID);
            _nameLabel.text = text; _nameShadow.text = text;
            bool visible = _definition.ContentEnabled && _hasBody && text.Length > 0;
            // 비활성 객체의 Renderer 범위는 비어 있으므로 측정하는 동안만 켭니다. 같은 프레임 안에서 최종 표시 여부로 되돌리므로
            // 렌더링 전에 끝나 화면에 잘못된 위치가 보이지 않습니다. 빈 문구는 준비 완료로 보지 않습니다.
            if (_hasBody && text.Length > 0)
            {
                _nameLabel.gameObject.SetActive(true);
                PlaceName();
            }
            _nameLabel.gameObject.SetActive(visible);
            _nameShadow.gameObject.SetActive(visible);
        }

        private void PlaceName()
        {
            // 정수 배치: 원하는 이름표 아랫변과 현재 글자 범위의 차이만큼 옮깁니다(TextMesh 정렬·피벗과 무관).
            Bounds label = _nameLabel.GetComponent<MeshRenderer>().bounds;
            long bottom = ToUnits(label.min.y), centerX = ToUnits(label.center.x);
            long targetBottom = checked(_bodyTop + IntegerPosition.FromData(_layout.NameGapUnits));
            Vector3 position = _nameLabel.transform.position;
            position.x += ToWorld(checked(_bodyCenterX - centerX));
            position.y += ToWorld(checked(targetBottom - bottom));
            _nameLabel.transform.position = position;
            long shadowOffset = IntegerPosition.FromData(NpcNameLayout.ShadowOffsetUnits);
            _nameShadow.transform.position = position + new Vector3(ToWorld(shadowOffset), -ToWorld(shadowOffset), .01f);
            long labelTop = checked(targetBottom + (ToUnits(label.max.y) - bottom));
            _promptBaseX = _bodyCenterX;
            _promptBaseY = checked(labelTop + IntegerPosition.FromData(_layout.PromptGapUnits));
            _hasPromptBase = true;
        }

        public bool TryGetPromptBase(out Vector2 position)
        {
            position = default;
            if (!_hasPromptBase || _nameLabel == null || !_nameLabel.gameObject.activeSelf) return false;
            position = new Vector2(ToWorld(_promptBaseX), ToWorld(_promptBaseY));
            return true;
        }

        private long ToUnits(float world) => SpatialRuntime.ToInteger(world, _layout.WorldUnitsPerTile);
        private float ToWorld(long units) => SpatialRuntime.ToWorld(units, _layout.WorldUnitsPerTile);

        private void ConnectDialogueEvents()
        {
            if (!isActiveAndEnabled || _dialogues == null || _dialogueScope != null) return;
            _dialogueScope = new SubscriptionScope();
            DialogueRunner runner = _dialogues;
            System.Action changed = _dialogueScope.Guard(() =>
            {
                if (this == null || !isActiveAndEnabled) return;
                // 표시 경계: 자기 이름표 갱신의 Unity 표시 오류만 여기서 기록합니다. 코어 명령을 호출하지 않습니다.
                try { RefreshNameText(); }
                catch (System.Exception error) { ErrorPopupHost.WriteError(error, this); }
                // 사용 가능·언어 변경은 이름표 표시 성공 여부와 무관하게 상호작용 소비자에게 알립니다.
                // 게임플레이 통지이므로 소비자 예외를 삼키지 않고 호출 흐름으로 전달합니다.
                NotifyChanged();
            });
            _dialogueScope.Add(() => LanguageSettings.Changed -= changed); LanguageSettings.Changed += changed;
            _dialogueScope.Add(() => runner.AvailabilityChanged -= changed); runner.AvailabilityChanged += changed;
        }

        // 맵 표식에 조립자가 붙일 때 씁니다.
        public void Configure(string speaker, string[] temporaryLines, string objective)
        {
            _speaker = speaker; _temporaryLines = temporaryLines; _objective = objective;
            NotifyChanged();
        }

        public override bool CanInteract => _definition == null || _definition.ContentEnabled && _definition.Type == "NPC"
            && _definition.DialogueID != 0 && _dialogues != null && _dialogues.CanPlay(_definition.DialogueID);
        public override int PromptTextId => 30064;

        public override void Interact(InteractionContext context)
        {
            if (_definition != null)
            {
                if (CanInteract && _definition.Type == "NPC") _dialogues.TryPlay(_definition.DialogueID, this);
                return;
            }
            var lines = new List<string>();
            foreach (int id in _lineIds) lines.Add(context.Text(id));
            if (lines.Count == 0) lines.AddRange(_temporaryLines);
            if (lines.Count == 0) throw new System.InvalidOperationException(name + "의 대사를 연결해 주세요.");
            if (_talked) lines = new List<string> { lines[lines.Count - 1] };
            bool first = !_talked;
            _talked = true;
            context.Textbox.Open(_speaker, lines, () => { if (first && _objective.Length > 0) context.Textbox.ShowObjective(_objective); });
        }

        // 구독 해제와 대화 취소 중 하나가 실패해도 나머지를 수행합니다(기존 SubscriptionScope.Cleanup 재사용).
        private void ReleaseDialogue()
        {
            SubscriptionScope scope = _dialogueScope; _dialogueScope = null;
            DialogueRunner runner = _dialogues;
            SubscriptionScope.Cleanup(() => scope?.Dispose(), () => runner?.Cancel(this));
        }
        protected override void OnEnable() { base.OnEnable(); ConnectDialogueEvents(); }
        // 상호작용 등록 해제(base.OnDisable)는 대화 정리가 실패해도 반드시 수행합니다.
        protected override void OnDisable() => SubscriptionScope.Cleanup(ReleaseDialogue, () => base.OnDisable());
    }

    // NPC 이름표 배치 공용 설정입니다. 조립자가 Inspector 정수값으로 공급하며 모든 NPC가 같이 씁니다.
    // 간격은 tile_x100, 배율은 분모 10000입니다.
    public readonly struct NpcNameLayout
    {
        public const int RatioScale = 10000;
        // 그림자 어긋남(tile_x100)입니다. 표시 장식이라 공급값이 아닌 고정값으로 둡니다.
        public const int ShadowOffsetUnits = 3;
        public int NameGapUnits { get; }
        public int NameScaleRatio { get; }
        public int PromptGapUnits { get; }
        public float WorldUnitsPerTile { get; }
        // 조립자 공급이 없는 기존 호출(임시 연결)의 값입니다. 0.3칸·기존 크기·0.1칸.
        public static NpcNameLayout Default => new NpcNameLayout(30, RatioScale, 10, 1f);

        public NpcNameLayout(int nameGapUnits, int nameScaleRatio, int promptGapUnits, float worldUnitsPerTile)
        {
            if (nameGapUnits < 0 || promptGapUnits < 0) throw new System.ArgumentOutOfRangeException(nameof(nameGapUnits), "간격은 0 이상이어야 합니다.");
            if (nameScaleRatio <= 0) throw new System.ArgumentOutOfRangeException(nameof(nameScaleRatio), "글자 배율은 양수여야 합니다.");
            if (worldUnitsPerTile <= 0f || float.IsNaN(worldUnitsPerTile) || float.IsInfinity(worldUnitsPerTile))
                throw new System.ArgumentOutOfRangeException(nameof(worldUnitsPerTile));
            NameGapUnits = nameGapUnits; NameScaleRatio = nameScaleRatio; PromptGapUnits = promptGapUnits; WorldUnitsPerTile = worldUnitsPerTile;
        }
    }
}
