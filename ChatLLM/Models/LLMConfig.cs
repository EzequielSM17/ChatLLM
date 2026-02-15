namespace Models
{
    public class LLMConfig
    {
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 500;
        public string Model { get; set; } = "local-model";
    }
}
