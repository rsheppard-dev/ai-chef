using AIChef.Server.Data;
using AIChef.Shared;
using Microsoft.AspNetCore.Mvc;
using Server.Services.Interfaces;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/recipe")]
    public class RecipeController : ControllerBase
    {
        private readonly IOpenAiApiService _openAiApiService;

        public RecipeController(IOpenAiApiService openAiApiService)
        {
            _openAiApiService = openAiApiService;
        }

        [HttpPost, Route("get-recipe-ideas")]
        public async Task<ActionResult<List<Idea>>> GetRecipeIdeas(RecipeParams recipeParams)
        {
            string mealTime = recipeParams.MealTime ?? "Breakfast";
            List<string?> ingredients = recipeParams.Ingredients
                .Where(i => !string.IsNullOrEmpty(i.Description))
                .Select(i => i.Description)
                .ToList();

            var ideas = await _openAiApiService.CreateRecipeIdeas(mealTime, ingredients!);

            return ideas;

            // return SampleData.RecipeIdeas;
        }

        [HttpPost, Route("get-recipe")]
        public async Task<ActionResult<Recipe>> GetRecipe(RecipeParams recipeParams)
        {
            List<string?> ingredients = recipeParams.Ingredients
                .Where(i => !string.IsNullOrEmpty(i.Description))
                .Select(i => i.Description)
                .ToList();

            string? title = recipeParams.SelectedIdea;

            if (string.IsNullOrEmpty(title))
            {
                return BadRequest("Title is required");
            }

            Recipe? recipe = await _openAiApiService.CreateRecipe(title, ingredients!);

            return Ok(recipe);

            // return SampleData.Recipe;
        }

        [HttpGet, Route("get-recipe-image")]
        public async Task<RecipeImage> GetRecipeImage(string title)
        {
            RecipeImage recipeImage = await _openAiApiService.GetRecipeImage(title);

            return recipeImage ?? SampleData.RecipeImage;
        }
    }
}