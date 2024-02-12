using AIChef.Shared;

namespace Server.Services.Interfaces
{
    public interface IOpenAiApiService
    {
        Task<List<Idea>> CreateRecipeIdeas(string mealTime, List<string> ingredients);
        Task<Recipe?> CreateRecipe(string title, List<string> ingredients);
        Task<RecipeImage> GetRecipeImage(string title);
    }
}