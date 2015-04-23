﻿using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Validators;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.SecurityModel;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Pages;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using BaseValidator = Sitecore.Data.Validators.BaseValidator;
using Control = Sitecore.Web.UI.HtmlControls.Control;
using Literal = Sitecore.Web.UI.HtmlControls.Literal;
using ValidatorCollection = Sitecore.Data.Validators.ValidatorCollection;

namespace ScheduledPublishing.sitecore.shell.Applications.ContentManager.Dialogs
{
    /// <summary>
    /// Schedule Publishing code-beside
    /// </summary>
    public class SchedulePublishingDialog : DialogForm
    {
        private readonly Database _database = Context.ContentDatabase;

        protected DateTimePicker PublishDateTime;
        protected Border ExistingSchedules;
        protected Literal ServerTime;
        protected Literal PublishTimeLit;
        protected Border PublishingTargets;
        protected Border Languages;
        protected Checkbox PublishChildren;
        protected Radiobutton SmartPublish;
        protected Radiobutton Republish;

        private Item _itemFromQueryString;
        private Item ItemFromQueryString
        {
            get
            {
                if (_itemFromQueryString != null)
                {
                    return this._itemFromQueryString;
                }

                this._itemFromQueryString = UIUtil.GetItemFromQueryString(_database);
                if (this._itemFromQueryString == null)
                {
                    Error.AssertItemFound(this._itemFromQueryString);
                }

                return this._itemFromQueryString;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");

            if (!Context.ClientPage.IsEvent)
            {
                this.SmartPublish.Checked = true;

                this.ServerTime.Text = "Current time on server: " + DateTime.Now;

                var isUnpublishing = bool.Parse(Context.Request.QueryString["unpublish"]);
                this.PublishTimeLit.Text = isUnpublishing ? "Unpiblish Time: " : "Publish Time: ";

                this.RenderExistingSchedules();
                this.BuildPublishingTargets();
                this.BuildLanguages();
            }

            base.OnLoad(e);
        }

        /// <summary>
        /// Create a task for publishing the selected item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        protected override void OnOK(object sender, EventArgs args)
        {
            //StartPublisher();
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");

            var isUnpublishing = bool.Parse(Context.Request.QueryString["unpublish"]);
            if (!string.IsNullOrEmpty(this.PublishDateTime.Value))
            {
                // Validate date chosen
                bool isDateValid = ValidateDateChosen();

                // Validate item to be published
                bool isItemValid = ValidateItemValidators();

                //Validate if item is publishable
                bool isPublishable = ValidatePublishable();

                // Create publishing task
                if (isDateValid && isItemValid && isPublishable)
                {
                    CreatePublishOptionsItem(this.ItemFromQueryString, isUnpublishing);
                }
            }

            base.OnOK(sender, args);
        }

        protected string JobHandle
        {
            get
            {
                return StringUtil.GetString(this.ServerProperties["JobHandle"]);
            }
            set
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                this.ServerProperties["JobHandle"] = (object)value;
            }
        }

        protected void StartPublisher()
        {
            Language[] languages = GetLanguages();
            Database[] publishingTargetDatabases = GetPublishingTargetDatabases().ToArray();
            bool @checked = this.PublishChildren.Checked;
            string id = this.ItemFromQueryString.ID.ToString();
            bool isIncremental = Context.ClientPage.ClientRequest.Form["PublishMode"] == "IncrementalPublish";
            bool isSmart = Context.ClientPage.ClientRequest.Form["PublishMode"] == "SmartPublish";

            this.JobHandle = (string.IsNullOrEmpty(id)
                ? (!isIncremental
                    ? (!isSmart
                        ? PublishManager.Republish(Client.ContentDatabase, publishingTargetDatabases, languages, Context.Language)
                        : PublishManager.PublishSmart(Client.ContentDatabase, publishingTargetDatabases, languages, Context.Language))
                    : PublishManager.PublishIncremental(Client.ContentDatabase, publishingTargetDatabases, languages, Context.Language))
                : PublishManager.PublishItem(Client.GetItemNotNull(id), publishingTargetDatabases, languages, @checked, isSmart)).ToString();
        }

        /// <summary>
        /// Renders available publishing targets.
        /// </summary>
        private void BuildPublishingTargets()
        {
            var publishingTargets = GetPublishingTargets();
            if (publishingTargets == null)
            {
                return;
            }

            foreach (Item target in publishingTargets)
            {
                var id = Control.GetUniqueID("pt_");
                var database = Database.GetDatabase(target[FieldIDs.PublishingTargetDatabase]);
                if (database == null)
                {
                    continue;
                }

                var targetInput = new HtmlGenericControl("input");
                targetInput.ID = id;
                targetInput.Attributes["type"] = "checkbox";
                targetInput.Disabled = !target.Access.CanWrite();
                this.PublishingTargets.Controls.Add(targetInput);

                var targetLabel = new HtmlGenericControl("label");
                targetLabel.Attributes["for"] = id;
                targetLabel.InnerText = string.Format("{0} ({1})", target.DisplayName, database.Name);
                this.PublishingTargets.Controls.Add(targetLabel);

                this.PublishingTargets.Controls.Add(new LiteralControl("<br>"));
            }
        }

        /// <summary>
        /// Renders available publishing languages.
        /// </summary>
        private void BuildLanguages()
        {
            var languages = LanguageManager.GetLanguages(_database);
            if (languages == null)
            {
                return;
            }

            foreach (var language in languages)
            {
                if (Settings.CheckSecurityOnLanguages)
                {
                    ID languageItemId = LanguageManager.GetLanguageItemId(language, _database);
                    Assert.IsNotNull(languageItemId, "languageItemId");
                    Item lang = _database.GetItem(languageItemId);
                    Assert.IsNotNull(lang, "lang");
                }

                var id = Control.GetUniqueID("lang_");

                var langInput = new HtmlGenericControl("input");
                langInput.ID = id;
                langInput.Attributes["type"] = "checkbox";
                langInput.Attributes["value"] = language.Name;
                this.Languages.Controls.Add(langInput);
               
                var langLabel = new HtmlGenericControl("label");
                langLabel.Attributes["for"] = id;
                langLabel.InnerText = language.CultureInfo.DisplayName;
                this.Languages.Controls.Add(langLabel);

                this.Languages.Controls.Add(new LiteralControl("<br>"));
            }
        }

        /// <summary>
        /// Displays a list of all already scheduled publishings' date and time for this item, ordered from most recent to furthest in time
        /// </summary>
        private void RenderExistingSchedules()
        {
            var pubhishingSchedulesFolder = _database.GetItem(Utils.Constants.PUBLISHING_SCHEDULES_PATH);
            Assert.ArgumentNotNull(pubhishingSchedulesFolder, "pubhishingSchedulesFolder");

            if (pubhishingSchedulesFolder.Children == null)
            {
                return;
            }

            var publishingTaskName = BuildPublishOptionsName(this.ItemFromQueryString.ID);
            var existingSchedules = 
                pubhishingSchedulesFolder.Children
                                         .Where(x => x.Name == publishingTaskName)
                                         .Select(x => DateUtil.IsoDateToDateTime(x["Schedule"].Substring(0, x["Schedule"].IndexOf('|'))).ToString(Context.Culture))
                                         .OrderBy(DateTime.Parse);

            var sbExistingSchedules = new StringBuilder();
            if (existingSchedules.Any())
            {
                foreach (var existingSchedule in existingSchedules)
                {
                    sbExistingSchedules.Append("<div style=\"padding:0px 0px 2px 0px; width=100%;\">" + existingSchedule + "</div>");
                    sbExistingSchedules.Append("<br />");
                }
            }
            else
            {
                sbExistingSchedules.Append("<div style=\"padding:0px 0px 2px 0px; width=100%;\">" + "This item has not been scheduled for publishing yet." + "</div>");
                sbExistingSchedules.Append("<br />");
            }

            this.ExistingSchedules.InnerHtml = sbExistingSchedules.ToString();
        }
        
        private void CreatePublishOptionsItem(Item itemFromQueryString, bool isUnpublishing)
        {
            try
            {
                using (new SecurityDisabler())
                {
                    DateTime dateChosen = DateUtil.IsoDateToDateTime(this.PublishDateTime.Value, DateTime.MinValue);
                    TemplateItem publishOptionsTemplate = _database.GetTemplate(new ID(Utils.Constants.PUBLISH_OPTIONS_TEMPLATE_ID));
                    var publishOptionsName = BuildPublishOptionsName(itemFromQueryString.ID);
                    Item optionsFolder = GetOrCreateFolder(dateChosen);
                    Item newPublishOptions = optionsFolder.Add(publishOptionsName, publishOptionsTemplate);
                    newPublishOptions.Editing.BeginEdit();
                    newPublishOptions["Items"] = itemFromQueryString.Paths.FullPath;
                    newPublishOptions["CreatedByEmail"] = Context.User.Profile.Email;
                    newPublishOptions["Unpublish"] = isUnpublishing ? 1.ToString() : string.Empty;
                    newPublishOptions["PublishMethod"] = this.SmartPublish.Checked ? "smart" : "republish";
                    newPublishOptions["PublishChildren"] = this.PublishChildren.Checked ? "1" : string.Empty;
                    newPublishOptions["PublishLanguages"] = string.Join("|", GetLanguages().Select(x => x.Name));
                    newPublishOptions["TargetDatabase"] = string.Join("|", GetPublishingTargetDatabases().Select(x => x.Name));

                    string format = "yyyyMMddTHHmmss";
                    newPublishOptions["Schedule"] = dateChosen.ToString(format) +
                        "|" +
                        dateChosen.AddHours(1).AddMinutes(1).ToString(format) +
                        "|127|00:60:00";

                    newPublishOptions.Editing.AcceptChanges();
                    newPublishOptions.Editing.EndEdit();

                    string action = isUnpublishing ? "unpublishing" : "publishing";
                    Log.Info(
                        "Created publish options: " + action + ": " + itemFromQueryString.Name + " " + itemFromQueryString.ID +
                        DateTime.Now, this);
                }
            }
            catch (Exception e)
            {
                string action = isUnpublishing ? "unpublishing" : "publishing";
                Log.Info(
                    "Failed creating publish options " + action + ": " + itemFromQueryString.Name + " " + itemFromQueryString.ID +
                    DateTime.Now + " " + e.ToString(), this);
            }
        }

        /// <summary>
        /// Get appropriate hour folder or create one if not present using the year/month/day/hour structure
        /// </summary>
        /// <param name="date">Date chosen for publishing</param>
        /// <returns>The hour folder as an item</returns>
        private Item GetOrCreateFolder(DateTime date)
        {
            Item publishOptionsFolder = _database.GetItem(Utils.Constants.PUBLISH_OPTIONS_FOLDER_PATH);
            string yearName = date.Year.ToString();
            string monthName = date.Month.ToString();
            string dayName = date.Day.ToString();
            string hourName = date.AddHours(1).Hour.ToString();

            TemplateItem folderTemplate = _database.GetTemplate(Utils.Constants.FOLDER_TEMPLATE_ID);

            Item yearFolder = publishOptionsFolder.Children.First(x => x.Name == yearName) ??
                              publishOptionsFolder.Add(yearName, folderTemplate);

            Item monthFolder = yearFolder.Children.First(x => x.Name == monthName) ??
                               yearFolder.Add(monthName, folderTemplate);

            Item dayFolder = monthFolder.Children.First(x => x.Name == dayName) ??
                             monthFolder.Add(dayName, folderTemplate);

            Item hourFolder = dayFolder.Children.First(x => x.Name == hourName) ??
                              dayFolder.Add(hourName, folderTemplate);

            return hourFolder;
        }

        /// <summary>
        /// Checks whether the item is publishable in the selected time.
        /// </summary>
        /// <returns>True if the item meets all date and time requirements for publishing</returns>
        private bool ValidatePublishable()
        {
            if (!this.ItemFromQueryString.Publishing.IsPublishable(
                DateUtil.IsoDateToDateTime(this.PublishDateTime.Value, DateTime.MinValue), false))
            {
                SheerResponse.Alert("Item is not publishable at that time.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if item has validator errors
        /// </summary>
        /// <returns>Always true if we want to be able to publish with validator errors. isValid if we want validator errors to prevent publishing.</returns>
        private bool ValidateItemValidators()
        {
            bool isValid = CheckValidation();
            if (!isValid)
            {
                SheerResponse.Alert("This item has validation errors. You may want to review them and schedule another publish.");
            }

            return true;
        }

        private bool ValidateDateChosen()
        {
            if (DateTime.Compare(DateUtil.IsoDateToDateTime(this.PublishDateTime.Value, DateTime.MinValue), DateTime.Now) <= 0)
            {
                SheerResponse.Alert("The date selected for publish has passed. Please select a future date.");
                return false;
            }

            return true;
        }

        private bool CheckValidation()
        {
            this.ItemFromQueryString.Fields.ReadAll();
            ValidatorCollection validators = ValidatorManager.GetGlobalValidatorsForItem(ValidatorsMode.Workflow, this.ItemFromQueryString);
            ValidatorCollection validatorsBar = ValidatorManager.GetGlobalValidatorsForItem(ValidatorsMode.ValidatorBar, this.ItemFromQueryString);
            ValidatorCollection validatorsButton = ValidatorManager.GetGlobalValidatorsForItem(ValidatorsMode.ValidateButton, this.ItemFromQueryString);
            ValidatorCollection validatorsGutter = ValidatorManager.GetGlobalValidatorsForItem(ValidatorsMode.Gutter, this.ItemFromQueryString);
            var options = new ValidatorOptions(true);
            ValidatorManager.Validate(validatorsBar, options);
            foreach (BaseValidator validator in validatorsBar)
            {
                if (validator.Result != ValidatorResult.Valid)
                {
                    return false;
                }
            }
            //return !validators.Any(x => x.Result != ValidatorResult.Valid);
            return true;
        }


        private static Language[] GetLanguages()
        {
            ArrayList arrayList = new ArrayList();
            foreach (string index in Context.ClientPage.ClientRequest.Form.Keys)
            {
                if (index != null && index.StartsWith("la_", StringComparison.InvariantCulture))
                    arrayList.Add(Language.Parse(Context.ClientPage.ClientRequest.Form[index]));
            }

            return arrayList.ToArray(typeof(Language)) as Language[];
        }

        private IEnumerable<Item> GetPublishingTargets()
        {
            var publishingTargets = PublishManager.GetPublishingTargets(_database);
            if (publishingTargets != null)
            {
                return publishingTargets;
            }

            Log.Info("No publishing targets found", this);
            return Enumerable.Empty<Item>();
        }

        private IEnumerable<Database> GetPublishingTargetDatabases()
        {
            var targets = GetPublishingTargets();
            if (targets == null)
            {
                return Enumerable.Empty<Database>();
            }

            return targets.Select(t => Database.GetDatabase(t[FieldIDs.PublishingTargetDatabase]));
        }

        private static string BuildPublishOptionsName(ID id)
        {
            return ItemUtil.ProposeValidItemName(string.Format("{0}_Options", id));
        }
    }
}