namespace AIChef.Shared
{
    public class RecipeParams
    {
        public string MealTime { get; set; } = "Breakfast";
        public List<Ingredient> Ingredients { get; set; } = new();
        public string? SelectedIdea { get; set; }
    }
}