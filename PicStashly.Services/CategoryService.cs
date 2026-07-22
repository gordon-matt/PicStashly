namespace PicStashly.Services;

public class CategoryService(
    ILogger<CategoryService> logger,
    IRepository<Category> categoryRepository,
    IRepository<Image> imageRepository) : ICategoryService
{
    public async Task<Result<List<CategoryItem>>> GetCategoriesAsync()
    {
        try
        {
            var categories = await categoryRepository.FindAsync(
                new SearchOptions<Category> { OrderBy = query => query.OrderBy(c => c.Name) });

            var imageCounts = await imageRepository.FindAsync(
                new SearchOptions<Image> { Query = i => i.CategoryId != null },
                i => i.CategoryId);

            var countByCategory = imageCounts
                .GroupBy(id => id)
                .ToDictionary(g => g.Key!.Value, g => g.Count());

            var items = categories
                .Select(c => new CategoryItem(c.Id, c.Name, countByCategory.GetValueOrDefault(c.Id)))
                .ToList();

            return Result.Success(items);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error retrieving categories");
            }

            return Result.Error("An error occurred while retrieving categories");
        }
    }

    public async Task<Result<CategoryItem>> CreateCategoryAsync(CreateCategoryRequest request)
    {
        try
        {
            string name = request.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Invalid([new ValidationError("Name", "Category name cannot be empty")]);
            }

            bool nameTaken = await categoryRepository.ExistsAsync(x => x.Name.ToLower() == name.ToLower());
            if (nameTaken)
            {
                return Result.Invalid([new ValidationError("Name", "A category with this name already exists")]);
            }

            var category = await categoryRepository.InsertAsync(new Category { Name = name });
            return Result.Success(new CategoryItem(category.Id, category.Name, 0));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error creating category");
            }

            return Result.Error("An error occurred while creating the category");
        }
    }

    public async Task<Result<CategoryItem>> RenameCategoryAsync(int categoryId, RenameCategoryRequest request)
    {
        try
        {
            string name = request.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Invalid([new ValidationError("Name", "Category name cannot be empty")]);
            }

            var category = await categoryRepository.FindOneAsync(categoryId);
            if (category is null)
            {
                return Result.NotFound("Category not found");
            }

            bool nameTaken = await categoryRepository.ExistsAsync(x => x.Id != categoryId && x.Name.ToLower() == name.ToLower());
            if (nameTaken)
            {
                return Result.Invalid([new ValidationError("Name", "A category with this name already exists")]);
            }

            category.Name = name;
            await categoryRepository.UpdateAsync(category);

            int usageCount = await imageRepository.CountAsync(x => x.CategoryId == categoryId);
            return Result.Success(new CategoryItem(category.Id, category.Name, usageCount));
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error renaming category {CategoryId}", categoryId);
            }

            return Result.Error("An error occurred while renaming the category");
        }
    }

    public async Task<Result> DeleteCategoryAsync(int categoryId)
    {
        try
        {
            var category = await categoryRepository.FindOneAsync(categoryId);
            if (category is null)
            {
                return Result.NotFound("Category not found");
            }

            // Images keep their files where they are; they just become uncategorized in the DB.
            // The FK is configured with ON DELETE SET NULL so this is handled at the database level.
            await categoryRepository.DeleteAsync(category);

            return Result.Success();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Error deleting category {CategoryId}", categoryId);
            }

            return Result.Error("An error occurred while deleting the category");
        }
    }
}
