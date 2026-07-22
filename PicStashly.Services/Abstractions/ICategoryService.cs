namespace PicStashly.Services.Abstractions;

public interface ICategoryService
{
    Task<Result<List<CategoryItem>>> GetCategoriesAsync();

    Task<Result<CategoryItem>> CreateCategoryAsync(CreateCategoryRequest request);

    Task<Result<CategoryItem>> RenameCategoryAsync(int categoryId, RenameCategoryRequest request);

    Task<Result> DeleteCategoryAsync(int categoryId);
}
