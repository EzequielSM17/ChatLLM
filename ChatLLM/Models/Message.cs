
namespace Models
{
    public class Message
    {
        public string Text { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        public Color Color => IsBot ? Colors.LightBlue : Colors.LightGreen;
    }
}
