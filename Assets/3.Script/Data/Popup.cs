// 최초 생성 데이터 정의입니다. 기존 파일은 자동 갱신하지 않으므로 시트 구조 변경 시 함께 수정하세요.
namespace HumanGhost.Data.Definitions
{
    public sealed class Popup
    {
        public int ID { get; }
        public int BodyTextID { get; }
        public int Button1TextID { get; }
        public int Button2TextID { get; }
        public int Button3TextID { get; }
        public int DefaultButton { get; }
        public int CancelButton { get; }
        public Popup(int id, int bodyTextID, int button1TextID, int button2TextID, int button3TextID, int defaultButton, int cancelButton)
        {
            ID = id;
            BodyTextID = bodyTextID;
            Button1TextID = button1TextID;
            Button2TextID = button2TextID;
            Button3TextID = button3TextID;
            DefaultButton = defaultButton;
            CancelButton = cancelButton;
        }
    }
}
