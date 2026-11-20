// 시트에서 자동 생성합니다. 매 배포 갱신하므로 직접 수정하지 마세요.
namespace HumanGhost.Data.Definitions
{
    public enum SoundCategory
    {
        Ambience = 1,
        Player = 2,
        Monster = 3,
        Boss = 4,
        Common = 5,
        Bgm = 6,
    }
    public static class SoundCategoryKeys
    {
        public static SoundCategory Parse(string key)
        {
            switch (key)
            {
                case "AMBIENCE": return SoundCategory.Ambience;
                case "PLAYER": return SoundCategory.Player;
                case "MONSTER": return SoundCategory.Monster;
                case "BOSS": return SoundCategory.Boss;
                case "COMMON": return SoundCategory.Common;
                case "BGM": return SoundCategory.Bgm;
                default: throw new System.InvalidOperationException("열거형 입력 키를 해석하지 못했습니다: " + key);
            }
        }
    }
}
