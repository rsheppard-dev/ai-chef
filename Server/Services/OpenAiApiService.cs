using System.Text.Json;
using AIChef.Shared;
using Server.ChatEndPoint;
using Server.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace Server.Services
{
    public class OpenAiApiService : IOpenAiApiService
    {
        private readonly IConfiguration _config;
        private static readonly string _baseUrl = "https://api.openai.com/v1";
        private static readonly HttpClient _httpClient = new();
        private readonly JsonSerializerOptions _jsonOptions = new();

        // build the function object so the AI will return JSON formatted object
        private static readonly ChatFunction.Parameter _recipeIdeaParameter = new()
        {
            // describes one Idea
            Type = "object",
            Required = new string[] { "Index", "Title", "Description" },
            Properties = new
            {
                // provide a type and description for each property of the Idea model
                Index = new ChatFunction.Property
                {
                    Type = "number",
                    Description = "A unique identifier for this object",
                },
                Title = new ChatFunction.Property
                {
                    Type = "string",
                    Description = "The name for a recipe to create"
                },
                Description = new ChatFunction.Property
                {
                    Type = "string",
                    Description = "A description of the recipe"
                }
            }
        };

        private static readonly ChatFunction _ideaFunction = new()
        {
            // describe the function we want an argument for from the AI
            Name = "CreateRecipe",
            // this description ensures we get 5 ideas back
            Description = "Generates recipes for each idea in an array of five recipe ideas",
            Parameters = new
            {
                // OpenAI requires that the parameters are an object, so we need to wrap our array in an object
                Type = "object",
                Properties = new
                {
                    Data = new // our array will come back in an object in the Data property
                    {
                        Type = "array",
                        // further ensures the AI will create 5 recipe ideas
                        Description = "An array of five recipe ideas",
                        Items = _recipeIdeaParameter
                    }
                }
            }
        };

        private static ChatFunction.Parameter _recipeParameter = new()
        {
            Type = "object",
            Description = "The recipe to display",
            Required = new[] { "title", "ingredients", "instructions", "summary" },
            Properties = new
            {
                Title = new
                {
                    Type = "string",
                    Description = "The title of the recipe to display",
                },
                Ingredients = new
                {
                    Type = "array",
                    Description = "An array of all the ingredients mentioned in the recipe instructions",
                    Items = new { Type = "string" }
                },
                Instructions = new
                {
                    Type = "array",
                    Description = "An array of each step for cooking this recipe",
                    Items = new { Type = "string" }
                },
                Summary = new
                {
                    Type = "string",
                    Description = "A summary description of what this recipe creates",
                },
            },
        };

        private static ChatFunction _recipeFunction = new()
        {
            Name = "DisplayRecipe",
            Description = "Displays the recipe from the parameter to the user",
            Parameters = new
            {
                Type = "object",
                Properties = new
                {
                    Data = _recipeParameter
                },
            }
        };

        public OpenAiApiService(IConfiguration config)
        {
            _config = config;
            string apiKey = _config["OpenAi:OpenAiKey"] ??
                Environment.GetEnvironmentVariable("OpenAiKey") ??
                throw new Exception("Unable to get API key.");
            
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);

            _jsonOptions.PropertyNameCaseInsensitive = true;
            _jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            _jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        }

        

        public async Task<List<Idea>> CreateRecipeIdeas(string mealTime, List<string> ingredientList)
        {
            string url = $"{_baseUrl}/chat/completions";
            string systemPrompt = "You are a world-renowned chef. I will send you list of ingredients and a meal time. You will respond with 5 ideas for dishes.";
            string userPrompt = string.Empty;
            string ingredientPrompt = string.Empty;

            string ingredients = string.Join(",", ingredientList);

            if (string.IsNullOrEmpty(ingredients))
            {
                ingredientPrompt = "Suggest some ingredients for me.";
            }
            else
            {
                ingredientPrompt = $"I have {ingredients}.";
            }

            userPrompt = $"The meal I want to cook is {mealTime}. {ingredientPrompt}";

            ChatMessage systemMessage = new()
            {
                Role = "system",
                Content = systemPrompt
            };

            ChatMessage userMessage = new()
            {
                Role = "user",
                Content = userPrompt
            };

            ChatRequest request = new()
            {
                Model = "gpt-3.5-turbo-0613",
                Messages = new[] { systemMessage, userMessage },
                Functions = new[] { _ideaFunction },
                FunctionCall = new { Name = _ideaFunction.Name }
            };

            HttpResponseMessage httpResponse = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions);

            ChatResponse? response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>();

            // get the first message in function
            ChatFunctionResponse? functionResponse = response?.Choices?.FirstOrDefault(m => m.Message?.FunctionCall is not null)?
                .Message?
                .FunctionCall;

            Result<List<Idea>>? ideasResult = new();

            try
            {
                if (functionResponse?.Arguments is not null)
                {
                    ideasResult = JsonSerializer.Deserialize<Result<List<Idea>>>(functionResponse.Arguments, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                ideasResult = new()
                {
                    Exception = ex,
                    ErrorMessage = await httpResponse.Content.ReadAsStringAsync()
                };
            }

            return ideasResult?.Data ?? new List<Idea>();
        }

        public async Task<Recipe?> CreateRecipe(string title, List<string> ingredients)
        {
            string url = $"{_baseUrl}/chat/completions";
            string systemPrompt = "You are a world-renowned chef. Create the recipe with ingredients, instructions and a summary.";
            string userPrompt = $"Create a {title} recipe.";

            ChatMessage userMessage = new()
            {
                Role = "user",
                Content = $"{systemPrompt} {userPrompt}"
            };

            ChatRequest request = new()
            {
                Model = "gpt-3.5-turbo-0613",
                Messages = new[] { userMessage },
                Functions = new[] { _recipeFunction },
                FunctionCall = new { Name = _recipeFunction.Name }
            };

            HttpResponseMessage httpResponse = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions);

            ChatResponse? response = await httpResponse.Content.ReadFromJsonAsync<ChatResponse?>();

            ChatFunctionResponse? functionResponse = response?.Choices?
                .FirstOrDefault(m => m.Message?.FunctionCall is not null)?
                .Message?
                .FunctionCall;

            Result<Recipe>? recipeResult = new();

            if (functionResponse?.Arguments is not null)
            {
                try
                {
                    recipeResult = JsonSerializer.Deserialize<Result<Recipe>>(functionResponse.Arguments, _jsonOptions);
                }
                catch (Exception ex)
                {
                    recipeResult = new()
                    {
                        Exception = ex,
                        ErrorMessage = await httpResponse.Content.ReadAsStringAsync()
                    };
                }
            }

            return recipeResult?.Data;
        }

        public async Task<RecipeImage> GetRecipeImage(string title)
        {
            string url = $"{_baseUrl}/images/generations";
            string userPrompt = string.Empty;

            userPrompt = $"Create a restaurant product shot for {title}.";

            ImageGenerationRequest request = new()
            {
                Prompt = userPrompt,
            };

            HttpResponseMessage httpResponse = await _httpClient.PostAsJsonAsync(url, request, _jsonOptions);
            
            RecipeImage? recipeImage = null;

            try
            {
                recipeImage = await httpResponse.Content.ReadFromJsonAsync<RecipeImage>();
            }
            catch (System.Exception)
            {
                Console.WriteLine("Error getting image");
            }

            return recipeImage ?? new RecipeImage();
        }
    }
}