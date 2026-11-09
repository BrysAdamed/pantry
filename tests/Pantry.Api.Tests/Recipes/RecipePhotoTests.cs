using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pantry.Api.Modules.Recipes;
using Pantry.Api.Tests.Infrastructure;
using SkiaSharp;

namespace Pantry.Api.Tests.Recipes;

[Collection(ApiCollection.Name)]
public sealed class RecipePhotoTests(PantryApiFactory factory) : IAsyncLifetime
{
    private const string RecipesUrl = "/api/recipes";

    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.DeleteAllUsersAsync();
        await factory.DeleteAllRecipesAsync();
        _client = await factory.CreateAdminAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Photos_require_a_session()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var recipe = await UploadAsync(id, TestPhotos.Jpeg(400, 300));
        var anonymous = factory.CreateBrowserClient();

        // Act
        var upload = await anonymous.PutAsync($"{RecipesUrl}/{id}/photo", Body(TestPhotos.Jpeg(40, 30)));
        var served = await anonymous.GetAsync(CardUrl(recipe));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, served.StatusCode);
    }

    [Fact]
    public async Task Uploaded_photo_is_served_as_a_square_card_and_a_detail_that_fits_and_can_be_cached()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");

        // Act
        var recipe = await UploadAsync(id, TestPhotos.Jpeg(3200, 1600));
        var card = await _client.GetAsync(CardUrl(recipe));
        var detail = await _client.GetAsync(DetailUrl(recipe));

        // Assert
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        Assert.Equal("image/webp", card.Content.Headers.ContentType?.MediaType);
        Assert.True(card.Headers.CacheControl?.Private);
        Assert.False(card.Headers.CacheControl?.NoStore);
        Assert.Equal(TimeSpan.FromDays(365), card.Headers.CacheControl?.MaxAge);
        Assert.Contains(card.Headers.CacheControl!.Extensions, e => e.Name == "immutable");
        using var cardImage = TestPhotos.Decode(await card.Content.ReadAsByteArrayAsync());
        Assert.Equal((480, 480), (cardImage.Width, cardImage.Height));

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var detailImage = TestPhotos.Decode(await detail.Content.ReadAsByteArrayAsync());
        Assert.Equal((1600, 800), (detailImage.Width, detailImage.Height));
        AssertRed(detailImage.GetPixel(100, 400));
        AssertBlue(detailImage.GetPixel(1500, 400));

        var listed = await _client.GetFromJsonAsync<JsonElement>(RecipesUrl);
        var item = Assert.Single(listed.GetProperty("items").EnumerateArray());
        Assert.Equal(CardUrl(recipe), item.GetProperty("photo").GetProperty("card").GetString());
    }

    [Fact]
    public async Task Photo_is_turned_upright_and_keeps_no_metadata()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var sideways = TestPhotos.Jpeg(400, 200, exifOrientation: 6);

        // Act
        var recipe = await UploadAsync(id, sideways);
        var detail = await _client.GetByteArrayAsync(DetailUrl(recipe));

        // Assert
        using var image = TestPhotos.Decode(detail);
        Assert.Equal((200, 400), (image.Width, image.Height));
        AssertRed(image.GetPixel(100, 20));
        AssertBlue(image.GetPixel(100, 380));
        Assert.Equal(-1, detail.AsSpan().IndexOf("Exif"u8));
        Assert.Equal(-1, detail.AsSpan().IndexOf("TestCam"u8));
    }

    [Fact]
    public async Task Png_is_accepted()
    {
        var id = await CreateAsync("Pierogi");

        var response = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(TestPhotos.Png(300, 300), "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Replacing_a_photo_removes_the_old_one()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var first = await UploadAsync(id, TestPhotos.Jpeg(400, 300));

        // Act
        var second = await UploadAsync(id, TestPhotos.Jpeg(300, 400));

        // Assert
        Assert.NotEqual(CardUrl(first), CardUrl(second));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(CardUrl(first))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(CardUrl(second))).StatusCode);
        Assert.Empty(FilesOf(first));
        Assert.Equal(2, FilesOf(second).Length);
    }

    [Fact]
    public async Task Removed_photo_is_gone_with_its_files()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var withPhoto = await UploadAsync(id, TestPhotos.Jpeg(400, 300));

        // Act
        var response = await _client.DeleteAsync($"{RecipesUrl}/{id}/photo");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recipe = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, recipe.GetProperty("photo").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(CardUrl(withPhoto))).StatusCode);
        Assert.Empty(FilesOf(withPhoto));
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync($"{RecipesUrl}/{id}/photo")).StatusCode);
    }

    [Fact]
    public async Task Archived_recipe_keeps_its_photo_and_can_get_a_new_one()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var withPhoto = await UploadAsync(id, TestPhotos.Jpeg(400, 300));

        // Act
        var archived = await _client.PostAsync($"{RecipesUrl}/{id}/archive", null);

        // Assert
        var recipe = await archived.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(CardUrl(withPhoto), recipe.GetProperty("photo").GetProperty("card").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(DetailUrl(withPhoto))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(TestPhotos.Jpeg(40, 30)))).StatusCode);
    }

    [Fact]
    public async Task Upload_that_is_not_a_photo_is_refused()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var gif = "GIF89a"u8.ToArray().Concat(new byte[64]).ToArray();
        var truncated = TestPhotos.Jpeg(400, 300)[..200];

        // Act
        var text = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body("not a photo"u8.ToArray(), "image/jpeg"));
        var other = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(gif, "image/gif"));
        var broken = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(truncated));
        var empty = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body([]));

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, text.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, other.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, broken.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, empty.StatusCode);
        var recipe = await _client.GetFromJsonAsync<JsonElement>($"{RecipesUrl}/{id}");
        Assert.Equal(JsonValueKind.Null, recipe.GetProperty("photo").ValueKind);
    }

    [Fact]
    public async Task Upload_over_the_size_limit_is_refused()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        var tooLarge = new byte[10 * 1024 * 1024 + 1];

        // Act
        var response = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(tooLarge));

        // Assert
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Photo_of_an_unknown_recipe_is_not_found()
    {
        var unknown = Guid.NewGuid();

        var upload = await _client.PutAsync($"{RecipesUrl}/{unknown}/photo", Body(TestPhotos.Jpeg(40, 30)));
        var remove = await _client.DeleteAsync($"{RecipesUrl}/{unknown}/photo");
        var served = await _client.GetAsync($"{RecipesUrl}/{unknown}/photos/{Guid.NewGuid()}/card");

        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, served.StatusCode);
    }

    [Fact]
    public async Task Unknown_photo_size_is_not_found()
    {
        var id = await CreateAsync("Pierogi");
        var recipe = await UploadAsync(id, TestPhotos.Jpeg(40, 30));

        var response = await _client.GetAsync(CardUrl(recipe).Replace("/card", "/huge"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Agent_sets_and_removes_a_photo_through_the_contract()
    {
        // Arrange
        var id = await CreateAsync("Pierogi");
        using var scope = factory.Services.CreateScope();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipesModule>();

        // Act
        var saved = await recipes.SetPhotoAsync(id, new MemoryStream(TestPhotos.Jpeg(400, 300)));
        var summary = await recipes.GetRecipeAsync(id);
        var notAPhoto = await recipes.SetPhotoAsync(id, new MemoryStream("text"u8.ToArray()));
        var unknown = await recipes.SetPhotoAsync(Guid.NewGuid(), new MemoryStream(TestPhotos.Jpeg(40, 30)));
        var removed = await recipes.RemovePhotoAsync(id);

        // Assert
        Assert.Equal(RecipePhotoResult.Saved, saved);
        Assert.True(summary!.HasPhoto);
        Assert.Equal(RecipePhotoResult.NotAPhoto, notAPhoto);
        Assert.Equal(RecipePhotoResult.RecipeNotFound, unknown);
        Assert.True(removed);
        Assert.False((await recipes.GetRecipeAsync(id))!.HasPhoto);
    }

    private async Task<Guid> CreateAsync(string name)
    {
        var response = await _client.PostAsJsonAsync(RecipesUrl, new { name, servings = 2 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> UploadAsync(Guid id, byte[] photo)
    {
        var response = await _client.PutAsync($"{RecipesUrl}/{id}/photo", Body(photo));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static ByteArrayContent Body(byte[] content, string contentType = "image/jpeg")
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return body;
    }

    private static string CardUrl(JsonElement recipe) => recipe.GetProperty("photo").GetProperty("card").GetString()!;

    private static string DetailUrl(JsonElement recipe) => recipe.GetProperty("photo").GetProperty("detail").GetString()!;

    private string[] FilesOf(JsonElement recipe)
    {
        var photoId = Guid.Parse(CardUrl(recipe).Split('/')[^2]);
        return Directory.Exists(factory.PhotosPath) ? Directory.GetFiles(factory.PhotosPath, $"{photoId:N}*") : [];
    }


    private static void AssertRed(SKColor colour) => Assert.True(colour.Red > 200 && colour.Blue < 60, $"Expected red, got {colour}.");

    private static void AssertBlue(SKColor colour) => Assert.True(colour.Blue > 200 && colour.Red < 60, $"Expected blue, got {colour}.");
}
