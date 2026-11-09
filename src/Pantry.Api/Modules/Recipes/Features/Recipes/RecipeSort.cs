namespace Pantry.Api.Modules.Recipes.Features.Recipes;

/// <summary>The order of the recipe library; the API code is the camelCase name (<c>notEatenLately</c>). Ties go by name.</summary>
public enum RecipeSort
{
    Name,

    /// <summary>Highest average rating first, then the most ratings; recipes never rated last.</summary>
    Rating,

    /// <summary>The most recently eaten first; recipes never eaten last.</summary>
    RecentlyEaten,

    /// <summary><i>Not eaten lately</i>: recipes never eaten first, then the longest since last eaten.</summary>
    NotEatenLately,

    /// <summary>The shortest prep time first; recipes without one last.</summary>
    PrepTime,
}
