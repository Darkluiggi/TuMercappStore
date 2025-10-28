﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Smartstore.Admin.Models;
using Smartstore.ComponentModel;
using Smartstore.Core.Common.Configuration;
using Smartstore.Core.Configuration;
using Smartstore.Core.Content.Media;
using Smartstore.Core.Localization;
using Smartstore.Core.Logging;
using Smartstore.Core.Messaging;
using Smartstore.Core.Rules.Filters;
using Smartstore.Core.Security;
using Smartstore.Core.Seo;
using Smartstore.Core.Stores;
using Smartstore.Data.Caching;
using Smartstore.Engine.Modularity;
using Smartstore.Web.Modelling.Settings;
using Smartstore.Web.Models.DataGrid;
using Smartstore.Web.Rendering;

namespace Smartstore.Admin.Controllers
{
    public partial class SettingController : AdminController
    {
        private readonly SmartDbContext _db;
        private readonly IProviderManager _providerManager;
        private readonly ICaptchaManager _captchaManager;
        private readonly ILocalizedEntityService _localizedEntityService;
        private readonly Lazy<IMediaTracker> _mediaTracker;

        public SettingController(
            SmartDbContext db,
            IProviderManager providerManager,
            ICaptchaManager captchaManager,
            ILocalizedEntityService localizedEntityService,
            Lazy<IMediaTracker> mediaTracker)
        {
            _db = db;
            _providerManager = providerManager;
            _captchaManager = captchaManager;
            _localizedEntityService = localizedEntityService;
            _mediaTracker = mediaTracker;
        }

        public async Task<IActionResult> ChangeStoreScopeConfiguration(int storeid, string returnUrl = "")
        {
            if (storeid != 0)
            {
                var stores = Services.StoreContext.GetAllStores();
                if (stores.Count < 2 || !stores.Any(x => x.Id == storeid))
                {
                    storeid = 0;
                }
            }

            Services.WorkContext.CurrentCustomer.GenericAttributes.AdminAreaStoreScopeConfiguration = storeid;
            await _db.SaveChangesAsync();

            return RedirectToReferrer(returnUrl, () => RedirectToAction("Index", "Home", new { area = "Admin" }));
        }

        #region General settings

        [LoadSetting]
        public async Task<IActionResult> GeneralCommon(
            int storeScope,
            StoreInformationSettings storeInformationSettings,
            SeoSettings seoSettings,
            DateTimeSettings dateTimeSettings,
            SecuritySettings securitySettings,
            CaptchaSettings captchaSettings,
            PdfSettings pdfSettings,
            LocalizationSettings localizationSettings,
            CompanyInformationSettings companySettings,
            ContactDataSettings contactDataSettings,
            BankConnectionSettings bankConnectionSettings,
            SocialSettings socialSettings,
            HomePageSettings homePageSettings,
            EmailAccountSettings emailAccountSettings)
        {
            var model = new GeneralCommonSettingsModel();

            // Map entities to model.
            MiniMapper.Map(storeInformationSettings, model.StoreInformationSettings);
            MiniMapper.Map(seoSettings, model.SeoSettings);
            MiniMapper.Map(dateTimeSettings, model.DateTimeSettings);
            MiniMapper.Map(securitySettings, model.SecuritySettings);
            MiniMapper.Map(captchaSettings, model.CaptchaSettings);
            MiniMapper.Map(pdfSettings, model.PdfSettings);
            MiniMapper.Map(localizationSettings, model.LocalizationSettings);
            MiniMapper.Map(companySettings, model.CompanyInformationSettings);
            MiniMapper.Map(contactDataSettings, model.ContactDataSettings);
            MiniMapper.Map(bankConnectionSettings, model.BankConnectionSettings);
            MiniMapper.Map(socialSettings, model.SocialSettings);
            MiniMapper.Map(homePageSettings, model.HomepageSettings);
            MiniMapper.Map(emailAccountSettings, model.EmailAccountSettings);

            #region SEO custom mapping

            // Fix for Disallows & Allows joined with comma in MiniMapper (we need NewLine).
            model.SeoSettings.ExtraRobotsDisallows = seoSettings.ExtraRobotsDisallows != null ? string.Join(Environment.NewLine, seoSettings.ExtraRobotsDisallows) : string.Empty;
            model.SeoSettings.ExtraRobotsAllows = seoSettings.ExtraRobotsAllows != null ? string.Join(Environment.NewLine, seoSettings.ExtraRobotsAllows) : string.Empty;

            model.SeoSettings.MetaTitle = seoSettings.MetaTitle;
            model.SeoSettings.MetaDescription = seoSettings.MetaDescription;
            model.SeoSettings.MetaKeywords = seoSettings.MetaKeywords;

            AddLocales(model.SeoSettings.Locales, (locale, languageId) =>
            {
                locale.MetaTitle = seoSettings.GetLocalizedSetting(x => x.MetaTitle, languageId, storeScope, false, false);
                locale.MetaDescription = seoSettings.GetLocalizedSetting(x => x.MetaDescription, languageId, storeScope, false, false);
                locale.MetaKeywords = seoSettings.GetLocalizedSetting(x => x.MetaKeywords, languageId, storeScope, false, false);
            });

            model.HomepageSettings.MetaTitle = homePageSettings.MetaTitle;
            model.HomepageSettings.MetaDescription = homePageSettings.MetaDescription;
            model.HomepageSettings.MetaKeywords = homePageSettings.MetaKeywords;

            AddLocales(model.HomepageSettings.Locales, (locale, languageId) =>
            {
                locale.MetaTitle = homePageSettings.GetLocalizedSetting(x => x.MetaTitle, languageId, storeScope, false, false);
                locale.MetaDescription = homePageSettings.GetLocalizedSetting(x => x.MetaDescription, languageId, storeScope, false, false);
                locale.MetaKeywords = homePageSettings.GetLocalizedSetting(x => x.MetaKeywords, languageId, storeScope, false, false);
            });

            #endregion

            await PrepareGeneralCommonConfigurationModelAsync(model);

            return View(model);
        }

        [Permission(Permissions.Configuration.Setting.Update)]
        [HttpPost, SaveSetting, FormValueRequired("save")]
        public async Task<IActionResult> GeneralCommon(
            GeneralCommonSettingsModel model,
            int storeScope,
            StoreInformationSettings storeInformationSettings,
            SeoSettings seoSettings,
            DateTimeSettings dateTimeSettings,
            SecuritySettings securitySettings,
            CaptchaSettings captchaSettings,
            PdfSettings pdfSettings,
            LocalizationSettings localizationSettings,
            CompanyInformationSettings companySettings,
            ContactDataSettings contactDataSettings,
            BankConnectionSettings bankConnectionSettings,
            SocialSettings socialSettings,
            HomePageSettings homePageSeoSettings,
            EmailAccountSettings emailAccountSettings)
        {
            if (!ModelState.IsValid)
            {
                return await GeneralCommon(storeScope,
                    storeInformationSettings,
                    seoSettings,
                    dateTimeSettings,
                    securitySettings,
                    captchaSettings,
                    pdfSettings,
                    localizationSettings,
                    companySettings,
                    contactDataSettings,
                    bankConnectionSettings,
                    socialSettings,
                    homePageSeoSettings,
                    emailAccountSettings);
            }

            ModelState.Clear();

            // Necessary before mapping
            var prevPdfLogoId = pdfSettings.LogoPictureId;

            // Map model to entities
            MiniMapper.Map(model.StoreInformationSettings, storeInformationSettings);
            MiniMapper.Map(model.SeoSettings, seoSettings);
            MiniMapper.Map(model.DateTimeSettings, dateTimeSettings);
            MiniMapper.Map(model.SecuritySettings, securitySettings);
            MiniMapper.Map(model.CaptchaSettings, captchaSettings);
            MiniMapper.Map(model.PdfSettings, pdfSettings);
            MiniMapper.Map(model.LocalizationSettings, localizationSettings);
            MiniMapper.Map(model.CompanyInformationSettings, companySettings);
            MiniMapper.Map(model.ContactDataSettings, contactDataSettings);
            MiniMapper.Map(model.BankConnectionSettings, bankConnectionSettings);
            MiniMapper.Map(model.SocialSettings, socialSettings);
            MiniMapper.Map(model.HomepageSettings, homePageSeoSettings);
            MiniMapper.Map(model.EmailAccountSettings, emailAccountSettings);

            #region POST mapping

            // Set CountryId explicitly else it can't be resetted.
            companySettings.CountryId = model.CompanyInformationSettings.CountryId ?? 0;

            // (Un)track PDF logo id
            await _mediaTracker.Value.TrackAsync(pdfSettings, prevPdfLogoId, x => x.LogoPictureId);

            seoSettings.MetaTitle = model.SeoSettings.MetaTitle;
            seoSettings.MetaDescription = model.SeoSettings.MetaDescription;
            seoSettings.MetaKeywords = model.SeoSettings.MetaKeywords;

            foreach (var localized in model.SeoSettings.Locales)
            {
                await _localizedEntityService.ApplyLocalizedSettingAsync(seoSettings, x => x.MetaTitle, localized.MetaTitle, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(seoSettings, x => x.MetaDescription, localized.MetaDescription, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(seoSettings, x => x.MetaKeywords, localized.MetaKeywords, localized.LanguageId, storeScope);
            }

            homePageSeoSettings.MetaTitle = model.HomepageSettings.MetaTitle;
            homePageSeoSettings.MetaDescription = model.HomepageSettings.MetaDescription;
            homePageSeoSettings.MetaKeywords = model.HomepageSettings.MetaKeywords;

            foreach (var localized in model.HomepageSettings.Locales)
            {
                await _localizedEntityService.ApplyLocalizedSettingAsync(homePageSeoSettings, x => x.MetaTitle, localized.MetaTitle, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(homePageSeoSettings, x => x.MetaDescription, localized.MetaDescription, localized.LanguageId, storeScope);
                await _localizedEntityService.ApplyLocalizedSettingAsync(homePageSeoSettings, x => x.MetaKeywords, localized.MetaKeywords, localized.LanguageId, storeScope);
            }

            await _db.SaveChangesAsync();

            #endregion

            // Does not contain any store specific settings.
            await Services.SettingFactory.SaveSettingsAsync(securitySettings);

            NotifySuccess(T("Admin.Configuration.Updated"));
            return RedirectToAction(nameof(GeneralCommon));
        }

        [Permission(Permissions.Configuration.Setting.Read)]
        [HttpPost, LoadSetting]
        public IActionResult TestSeoNameCreation(SeoSettings settings, GeneralCommonSettingsModel model)
        {
            // We always test against persisted settings.
            var result = SlugUtility.Slugify(
                model.SeoSettings.TestSeoNameCreation,
                settings.ConvertNonWesternChars,
                settings.AllowUnicodeCharsInUrls,
                SeoSettings.CreateCharConversionMap(settings.SeoNameCharConversion));

            return Content(result);
        }

        private async Task PrepareGeneralCommonConfigurationModelAsync(GeneralCommonSettingsModel model)
        {
            ViewBag.AvailableTimeZones = Services.DateTimeHelper.GetSystemTimeZones()
                .ToSelectListItems(Services.DateTimeHelper.DefaultStoreTimeZone.Id);

            var emailAccounts = await _db.EmailAccounts
                .AsNoTracking()
                .AsNoCaching()
                .OrderBy(x => x.Id)
                .ToListAsync();

            ViewBag.EmailAccounts = emailAccounts
                .Select(x => new SelectListItem { Text = x.FriendlyName, Value = x.Id.ToString(), Selected = x.Id == model.EmailAccountSettings.DefaultEmailAccountId })
                .ToList();

            ViewBag.Salutations = new List<SelectListItem>
            {
                CreateItem("Admin.Address.Salutation.Mr"),
                CreateItem("Admin.Address.Salutation.Mrs")
            };

            var resRoot = "Admin.Configuration.Settings.GeneralCommon.CompanyInformationSettings.ManagementDescriptions.";
            ViewBag.ManagementDescriptions = new List<SelectListItem>();
            ViewBag.ManagementDescriptions.AddRange(new[]
            {
                CreateItem(resRoot + "Manager"),
                CreateItem(resRoot + "Shopkeeper"),
                CreateItem(resRoot + "Procurator"),
                CreateItem(resRoot + "Shareholder"),
                CreateItem(resRoot + "AuthorizedPartner"),
                CreateItem(resRoot + "Director"),
                CreateItem(resRoot + "ManagingPartner")
            });

            ViewBag.AvailableMetaContentValues = new List<SelectListItem>
            {
                new() { Text = "index", Value = "index" },
                new() { Text = "noindex", Value = "noindex" },
                new() { Text = "index, follow", Value = "index, follow" },
                new() { Text = "index, nofollow", Value = "index, nofollow" },
                new() { Text = "noindex, follow", Value = "noindex, follow" },
                new() { Text = "noindex, nofollow", Value = "noindex, nofollow" }
            };

            model.CaptchaSettings.AvailableProviders = _captchaManager.ListProviders()
                .Select(x => new GeneralCommonSettingsModel.CaptchaProviderModel
                {
                    SystemName = x.Metadata.SystemName,
                    FriendlyName = x.Metadata.FriendlyName,
                    IsConfigured = x.Value.IsConfigured,
                    ConfigureUrl = x.Metadata.IsConfigurable
                        ? Url.Action(((IConfigurable)x.Value).GetConfigurationRoute())
                        : null
                })
                .ToList();

            var selectedTargets = model.CaptchaSettings.ShowOn ?? [];
            var captchaTargetOptions = CaptchaSettings.Targets.GetDisplayResourceKeys()
                .Select(x => new SelectListItem
                {
                    Value = x.Key,
                    Text = T(x.Value).Value
                })
                .ToList();

            ViewBag.CaptchaShowOnOptions = new MultiSelectList(
                captchaTargetOptions, 
                nameof(SelectListItem.Value), 
                nameof(SelectListItem.Text), 
                selectedTargets);

            SelectListItem CreateItem(string resourceKey)
            {
                var value = T(resourceKey).Value.EmptyNull();
                return new() { Text = value, Value = value };
            }
        }

        #endregion

        #region All settings

        [Permission(Permissions.Configuration.Setting.Read)]
        public IActionResult AllSettings(SettingListModel model)
        {
            model.IsSingleStoreMode = Services.StoreContext.IsSingleStoreMode();
            return View(model);
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Setting.Read)]
        public async Task<IActionResult> SettingList(GridCommand command, SettingListModel model)
        {
            var stores = Services.StoreContext.GetAllStores();
            var query = _db.Settings.AsNoTracking();

            if (model.SearchSettingName.HasValue())
            {
                query = query.ApplySearchFilterFor(x => x.Name, model.SearchSettingName);
            }

            if (model.SearchSettingValue.HasValue())
            {
                query = query.ApplySearchFilterFor(x => x.Value, model.SearchSettingValue);
            }

            if (model.SearchStoreId != 0)
            {
                query = query.Where(x => x.StoreId == model.SearchStoreId);
            }

            var settings = await query
                .OrderBy(x => x.Name)
                .ApplyGridCommand(command)
                .ToPagedList(command)
                .LoadAsync();

            var allStoresStr = T("Admin.Common.StoresAll").Value;
            var allStoreNames = Services.StoreContext.GetAllStores().ToDictionary(x => x.Id, x => x.Name);

            var rows = settings
                .Select(x => new SettingModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Value = x.Value,
                    StoreId = x.StoreId,
                    Store = x.StoreId == 0 ? allStoresStr : allStoreNames.Get(x.StoreId).NaIfEmpty()
                })
                .ToList();

            var gridModel = new GridModel<SettingModel>
            {
                Rows = rows,
                Total = await settings.GetTotalCountAsync()
            };

            return Json(gridModel);
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Setting.Update)]
        public async Task<IActionResult> SettingUpdate(SettingModel model)
        {
            model.Name = model.Name?.Trim();
            model.Value = model.Value?.Trim();
            model.StoreId = model.StoreId.GetValueOrDefault();

            var success = false;
            var setting = await _db.Settings.FindByIdAsync(model.Id);

            if (setting != null)
            {
                await MapperFactory.MapAsync(model, setting);
                await _db.SaveChangesAsync();
                success = true;
            }

            return Json(new { success });
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Setting.Create)]
        public async Task<IActionResult> SettingInsert(SettingModel model)
        {
            model.Name = model.Name?.Trim();
            model.Value = model.Value?.Trim();

            var success = true;
            var setting = new Setting();

            await MapperFactory.MapAsync(model, setting);
            _db.Settings.Add(setting);
            await _db.SaveChangesAsync();

            return Json(new { success });
        }

        [HttpPost]
        [Permission(Permissions.Configuration.Setting.Delete)]
        public async Task<IActionResult> SettingDelete(GridSelection selection)
        {
            var entities = await _db.Settings.GetManyAsync(selection.GetEntityIds(), true);
            if (entities.Count > 0)
            {
                _db.Settings.RemoveRange(entities);
                await _db.SaveChangesAsync();

                Services.ActivityLogger.LogActivity(
                    KnownActivityLogTypes.DeleteSetting,
                    T("ActivityLog.DeleteSetting"),
                    string.Join(", ", entities.Select(x => x.Name)));
            }

            return Json(new { Success = true, entities.Count });
        }

        #endregion
    }
}