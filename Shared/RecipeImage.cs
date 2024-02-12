namespace AIChef.Shared
{
    public class RecipeImage
    {
        public int Created { get; set; }
        public ImageData[]? Data { get; set; }
    }

    public class ImageData
    {
        public string? Url { get; set; }
    }
}