namespace SpireUI
{
    public interface IUiElement
    {
        void Initialize(BaseUIView owner);
        void OnShow();
        void OnHide();
        void Release();
    }
}
