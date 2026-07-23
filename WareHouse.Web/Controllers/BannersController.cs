using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WareHouse.Web.Identity;
using WareHouse.Web.Services;
using WareHouse.Web.ViewModels;

namespace WareHouse.Web.Controllers;

[Authorize(Roles = AppRoles.AdminOrManager)]
public sealed class BannersController(StoreBannerService banners) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await banners.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new BannerEditViewModel { IsActive = true });
        var banner = await banners.GetAsync(id.Value);
        if (banner is null) return NotFound();
        return View(new BannerEditViewModel
        {
            Id = banner.Id,
            Title = banner.Title,
            TitleEn = banner.TitleEn,
            TitleDe = banner.TitleDe,
            Url = banner.Url,
            SortOrder = banner.SortOrder,
            IsActive = banner.IsActive,
            ExistingImageUrl = banners.ImageUrl(banner.ImagePath)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Edit(BannerEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await RestoreExistingImageAsync(model);
            return View(model);
        }

        var result = await banners.SaveAsync(model);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await RestoreExistingImageAsync(model);
            return View(model);
        }

        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await banners.DeleteAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task RestoreExistingImageAsync(BannerEditViewModel model)
    {
        if (model.Id <= 0) return;
        var banner = await banners.GetAsync(model.Id);
        if (banner is not null) model.ExistingImageUrl = banners.ImageUrl(banner.ImagePath);
    }
}
