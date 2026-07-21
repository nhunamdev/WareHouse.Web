using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WareHouse.Data;
using WareHouse.Web.Identity;
using WareHouse.Web.Models;
using WareHouse.Web.Services;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public class ProductsController(
    WareHouseServices services,
    ProductImageService productImages,
    IOptionsSnapshot<ProductCatalogSettings> catalogOptions,
    IOptionsSnapshot<ProductImageStorageOptions> imageOptions) : Controller
{
    private readonly WareHouseServices _services = services;
    private readonly ProductImageService _productImages = productImages;
    private readonly ProductCatalogSettings _catalogOptions = catalogOptions.Value;
    private readonly ProductImageStorageOptions _imageOptions = imageOptions.Value;

    public async Task<IActionResult> Index(string? keyword, string? sort, string? direction, int page = 1)
    {
        ViewBag.Keyword = keyword;
        ViewBag.Sort = sort;
        ViewBag.Direction = direction;
        var products = await _services.GetProductsAsync(keyword, page, sort: sort, direction: direction);
        var summary = await _services.GetProductCatalogSummaryAsync();
        ViewBag.ProductCount = summary.ProductCount;
        ViewBag.ActiveItemCount = summary.ActiveItemCount;
        ViewBag.ImageBaseUrl = _imageOptions.RequestPath.TrimEnd('/');
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        var model = new ProductEditViewModel();
        if (!id.HasValue)
        {
            model.Product.IsActive = true;
            model.Variants.Add(new ProductItemInput());
        }
        else
        {
            var product = await _services.GetProductAsync(id.Value);
            if (product is null) return NotFound();
            model.Product = product;
            model.ExistingImages = product.Images
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .ToList();
            model.Variants = product.Items
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => new ProductItemInput
                {
                    Id = x.Id,
                    CostPrice = x.CostPrice,
                    SalePrice = x.SalePrice,
                    AttributeValueIds = x.ItemAttributes
                        .Select(attribute => attribute.AttributeValueId)
                        .ToList()
                })
                .ToList();
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(30 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 30 * 1024 * 1024)]
    public async Task<IActionResult> Edit(ProductEditViewModel model)
    {
        // Mã sản phẩm không còn là dữ liệu người dùng phải nhập. Trường này được
        // để trống khi tạo mới để service tự sinh, vì vậy không áp dụng quy tắc
        // "required" ngầm của ASP.NET cho string non-nullable lên ô ẩn này.
        ModelState.Remove($"{nameof(ProductEditViewModel.Product)}.{nameof(Product.Code)}");
        if (model.Product.Code is null)
            model.Product.Code = string.Empty;

        if (!ModelState.IsValid)
        {
            await FillOptionsAsync(model);
            return View(model);
        }

        var removedImageIds = ParseIds(model.RemovedImageIds);
        var imageValidation = await _productImages.ValidateAsync(
            model.Product.Id, removedImageIds, model.NewImages);
        if (!imageValidation.Success)
        {
            ModelState.AddModelError(string.Empty, imageValidation.Message);
            await FillOptionsAsync(model);
            return View(model);
        }

        var result = await _services.SaveProductWithItemsAsync(model.Product, model.Variants);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await FillOptionsAsync(model);
            return View(model);
        }

        var productId = checked((int)(result.Id ?? model.Product.Id));
        var imageResult = await _productImages.ApplyAsync(
            productId,
            removedImageIds,
            model.NewImages,
            model.ImageOrder,
            model.PrimaryImageKey,
            model.ImageAssignments);
        if (!imageResult.Success)
        {
            TempData["Error"] = $"{result.Message} Tuy nhiên, {imageResult.Message}";
            return RedirectToAction(nameof(Edit), new { id = productId });
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var imagePaths = await _productImages.GetRelativePathsAsync(id);
        var result = await _services.DeleteProductAsync(id);
        if (result.Success) await _productImages.DeletePhysicalFilesAsync(imagePaths);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task FillOptionsAsync(ProductEditViewModel model)
    {
        var savedCategories = await _services.GetProductCategoriesAsync();
        var savedUnits = await _services.GetProductUnitsAsync();

        ViewBag.Categories = MergeOptions(_catalogOptions.Categories, savedCategories);
        ViewBag.Units = MergeOptions(_catalogOptions.Units, savedUnits);
        model.AttributeGroups = await _services.GetActiveAttributesAsync();
        model.ImageBaseUrl = _imageOptions.RequestPath.TrimEnd('/');
        if (model.Product.Id > 0)
            model.ExistingImages = await _productImages.GetImagesAsync(model.Product.Id);
    }

    private static IReadOnlyCollection<int> ParseIds(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

    private static IReadOnlyList<string> MergeOptions(
        IEnumerable<string> configuredOptions,
        IEnumerable<string> savedOptions) =>
        configuredOptions.Concat(savedOptions)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
