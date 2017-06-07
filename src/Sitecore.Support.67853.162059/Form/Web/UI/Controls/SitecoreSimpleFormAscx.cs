﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web.UI;
using Sitecore.Diagnostics;
using Sitecore.Form.Core.Client.Submit;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Data;
using Sitecore.Form.Core.Pipelines.FormSubmit;
using Sitecore.Form.Core.Utility;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Forms.Core.Data;
using Sitecore.Pipelines;
using Sitecore.Support.Forms.Core.Handlers;
using Sitecore.WFFM.Abstractions.Actions;
using Sitecore.WFFM.Abstractions.Dependencies;
using BaseValidator = System.Web.UI.WebControls.BaseValidator;
using Sitecore.Data.Items;
using Sitecore.Forms.Core.Rules;
using Sitecore.Form.Core.Renderings.Controls;
using Sitecore.Form.Core.Ascx.Controls;
using System.Reflection;

namespace Sitecore.Support.Form.Web.UI.Controls
{
  public class SitecoreSimpleFormAscx : Sitecore.Form.Web.UI.Controls.SitecoreSimpleFormAscx
  {
    protected override void OnClick(object sender, EventArgs e)
    {
      Assert.ArgumentNotNull(sender, "sender");
      var sessionValue = SessionUtil.GetSessionValue<object>(AntiCsrf.ID);

      if ((sessionValue == null) || (sessionValue.ToString() != AntiCsrf.Value))
      {
        var failures = new ExecuteResult.Failure[1];
        var failure = new ExecuteResult.Failure
        {
          ErrorMessage = "WFFM: Forged request detected!"
        };

        failures[0] = failure;

        var args = new SubmittedFormFailuresArgs(FormID, failures)
        {
          Database = StaticSettings.ContextDatabase.Name
        };

        CorePipeline.Run("errorSubmit", args);

        Log.Error("WFFM: Forged request detected!", this);
        OnRefreshError((from f in args.Failures select f.ErrorMessage).ToArray());
      }
      else
      {
        UpdateSubmitAnalytics();
        UpdateSubmitCounter();

        var flag = false;
        var collection = (Page ?? new Page()).GetValidators(((Control)sender).ID);

        if (collection.FirstOrDefault(v => (!v.IsValid && (v is IAttackProtection))) != null)
        {
          collection.ForEach(delegate (IValidator v)
          {
            if (!v.IsValid && !(v is IAttackProtection))
            {
              v.IsValid = true;
            }
          });
        }

        if ((Page != null) && Page.IsValid)
        {
          RequiredMarkerProccess(this, true);
          var list = new List<IActionDefinition>();
          CollectActions(this, list);

          try
          {
            DependenciesManager.Resolve<FormDataHandler>().ProcessForm(FormID, GetChildState().ToArray(), list.ToArray());

            OnSuccessSubmit();
            OnSucceedValidation(new EventArgs());
            OnSucceedSubmit(new EventArgs());
          }
          catch (ThreadAbortException)
          {
            flag = true;
          }
          catch (ValidatorException exception)
          {
            OnRefreshError(new[] { exception.Message });
          }
          catch (FormSubmitException exception2)
          {
            flag = true;
            OnRefreshError((from f in exception2.Failures select f.ErrorMessage).ToArray());
          }
          catch (Exception exception3)
          {
            try
            {
              var failureArray2 = new ExecuteResult.Failure[1];

              var failure2 = new ExecuteResult.Failure
              {
                ErrorMessage = exception3.ToString(),
                StackTrace = exception3.StackTrace
              };

              failureArray2[0] = failure2;
              var args3 = new SubmittedFormFailuresArgs(FormID, failureArray2)
              {
                Database = StaticSettings.ContextDatabase.Name
              };

              CorePipeline.Run("errorSubmit", args3);
              OnRefreshError((from f in args3.Failures select f.ErrorMessage).ToArray());
            }
            catch (Exception exception4)
            {
              Log.Error(exception4.Message, exception4, this);
            }
            flag = true;
          }
        }
        else
        {
          SetFocusOnError();
          TrackValdationEvents(sender, e);
          RequiredMarkerProccess(this, false);
        }
        EventCounter.Value = (DependenciesManager.AnalyticsTracker.EventCounter + 1).ToString();

        if (flag)
        {
          OnSucceedValidation(new EventArgs());
        }

        OnFailedSubmit(new EventArgs());
      }
    }

    private void OnFailedSubmit(EventArgs e)
    {
      var failedSubmit = FailedSubmit;
      failedSubmit?.Invoke(this, e);
    }

    private void UpdateSubmitAnalytics()
    {
      if (!IsAnalyticsEnabled || FastPreview) return;

      DependenciesManager.AnalyticsTracker.BasePageTime = RenderedTime;
      DependenciesManager.AnalyticsTracker.TriggerEvent(WFFM.Abstractions.Analytics.IDs.FormSubmitEventId, "Form Submit", FormID, string.Empty, FormID.ToString());
    }

    private void SetFocusOnError()
    {
      var validator = (BaseValidator) Page?.Validators.FirstOrDefault(v =>
      {
        var baseValidator = v as BaseValidator;
        return baseValidator != null && baseValidator.IsFailedAndRequireFocus();
      });

      if (validator == null) return;

      if (!string.IsNullOrEmpty(validator.Text))
      {
        var controlToValidate = validator.GetControlToValidate();

        if (controlToValidate == null) return;
        SetFocus(validator.ClientID, controlToValidate.ClientID);
      }
      else
      {
        var control = FindControl(BaseID + prefixErrorID);

        if (control == null) return;
        SetFocus(control.ClientID, null);
      }
    }

    private void UpdateSubmitCounter()
    {
      if (RobotDetection.Session.Enabled) SubmitCounter.Session.AddSubmit(FormID, RobotDetection.Session.MinutesInterval);
      if (!RobotDetection.Server.Enabled) return;

      SubmitCounter.Server.AddSubmit(FormID, RobotDetection.Server.MinutesInterval);
    }

    private void OnSucceedValidation(EventArgs args)
    {
      var succeedValidation = SucceedValidation;
      succeedValidation?.Invoke(this, args);
    }

    private void OnSucceedSubmit(EventArgs e)
    {
      var succeedSubmit = SucceedSubmit;
      succeedSubmit?.Invoke(this, e);
    }

    private void TrackValdationEvents(object sender, EventArgs e)
    {
      if (!IsDropoutTrackingEnabled) return;
      OnTrackValidationEvent(sender, e);
    }
    protected new void Expand()
    {
        Item[] sections = this.FormItem.Sections;
        if (sections.Length == 1 && sections[0].TemplateID != IDs.SectionTemplateID)
        {
            Sitecore.Support.Form.Web.UI.Controls.FormSection formSection = new Sitecore.Support.Form.Web.UI.Controls.FormSection(sections[0], this.FormItem[sections[0].ID.ToShortID().ToString()], false, this.Submit.ID, base.FastPreview)
            {
                ReadQueryString = this.ReadQueryString,
                DisableWebEditing = this.DisableWebEditing,
                RenderingParameters = this.Parameters
            };
            ReflectionUtils.SetXmlProperties(formSection, sections[0][Sitecore.Form.Core.Configuration.FieldIDs.FieldParametersID], true);
            ReflectionUtils.SetXmlProperties(formSection, sections[0][Sitecore.Form.Core.Configuration.FieldIDs.FieldLocalizeParametersID], true);
            this.FieldContainer.Controls.Add(formSection);
            return;
        }
        Item[] array = sections;
        for (int i = 0; i < array.Length; i++)
        {
            Item item = array[i];
            Sitecore.Support.Form.Web.UI.Controls.FormSection formSection2 = new Sitecore.Support.Form.Web.UI.Controls.FormSection(item, this.FormItem[item.ID.ToShortID().ToString()], true, this.Submit.ID, base.FastPreview)
            {
                ReadQueryString = this.ReadQueryString,
                DisableWebEditing = this.DisableWebEditing
            };
            ReflectionUtils.SetXmlProperties(formSection2, item[Sitecore.Form.Core.Configuration.FieldIDs.FieldParametersID], true);
            ReflectionUtils.SetXmlProperties(formSection2, item[Sitecore.Form.Core.Configuration.FieldIDs.FieldLocalizeParametersID], true);
            Rule.Run(item[Sitecore.Form.Core.Configuration.FieldIDs.ConditionsFieldID], formSection2);
            this.FieldContainer.Controls.Add(formSection2);
        }
    }

        protected override void OnInit(EventArgs e)
        {
            Assert.IsNotNull(base.FormItem, "FormItem");
            if (this.Page == null)
            {
                this.Page = WebUtil.GetPage();
                ReflectionUtils.SetField(typeof(Page), this.Page, "_enableEventValidation", false);
            }
            this.Page.EnableViewState = true;
            ThemesManager.RegisterCssScript(this.Page, base.FormItem.InnerItem, Sitecore.Context.Item);
            // Use reflection to initialize object
            PropertyInfo _formTitle = typeof(FormTitle).GetProperty("Item");
            _formTitle.SetValue(Title, base.FormItem.InnerItem);
            //base.title.Item = base.FormItem.InnerItem;
            base.title.SetTagKey(base.FormItem.TitleTag);
            base.title.DisableWebEditing = base.DisableWebEditing;
            base.title.Parameters = base.Parameters;
            base.title.FastPreview = base.FastPreview;
            // Use reflection to initialize object
            PropertyInfo _formIntroduction = typeof(FormIntroduction).GetProperty("Item");
            _formIntroduction.SetValue(Intro, base.FormItem.InnerItem);
            //base.intro.Item = base.FormItem.InnerItem;
            base.intro.DisableWebEditing = base.DisableWebEditing;
            base.intro.Parameters = base.Parameters;
            base.intro.FastPreview = base.FastPreview;
            // Use reflection to initialize object
            PropertyInfo _formSubmit = typeof(FormSubmit).GetProperty("Item");
            _formSubmit.SetValue(Submit, base.FormItem.InnerItem);
            //base.submit.Item = base.FormItem.InnerItem;
            base.submit.ID = this.ID + "_submit";
            base.submit.DisableWebEditing = base.DisableWebEditing;
            base.submit.Parameters = base.Parameters;
            base.submit.FastPreview = base.FastPreview;
            base.submit.ValidationGroup = base.submit.ID;
            base.submit.Click += new EventHandler(this.OnClick);
            if (base.FastPreview)
            {
                base.summary.Visible = false;
            }
            base.summary.ID = SimpleForm.prefixSummaryID;
            base.summary.ValidationGroup = base.submit.ID;
            base.submitSummary.ID = this.ID + SimpleForm.prefixErrorID;
            // Invoke overriden method to applay default css class for section
            Expand();
            // Use reflection to initialize object
            PropertyInfo _formFooter = typeof(FormFooter).GetProperty("Item");
            _formFooter.SetValue(Footer, base.FormItem.InnerItem);
            //base.footer.Item = base.FormItem.InnerItem;
            base.footer.DisableWebEditing = base.DisableWebEditing;
            base.footer.Parameters = base.Parameters;
            base.footer.FastPreview = base.FastPreview;
            base.EventCounter.ID = this.ID + SimpleForm.prefixEventCountID;
            this.Controls.Add(base.EventCounter);
            base.AntiCsrf.ID = this.ID + SimpleForm.PrefixAntiCsrfId;
            this.Controls.Add(base.AntiCsrf);
            object sessionValue = SessionUtil.GetSessionValue<object>(base.AntiCsrf.ID);
            if (sessionValue == null)
            {
                sessionValue = Guid.NewGuid().ToString();
                SessionUtil.SetSessionValue(base.AntiCsrf.ID, sessionValue);
            }
            if (!base.IsPostBack || !base.Request.Form.AllKeys.Any<string>(k => ((k != null) && k.Contains(base.submit.ID))))
            {
                base.AntiCsrf.Value = sessionValue.ToString();
            }
        }

        [Obsolete("Use SubmitSummary")]
        protected new Label Error
        {
            get
            {
                return null;
            }
        }
        protected override Control FieldContainer
        {
            get
            {
                return base.fieldContainer;
            }
        }
        protected override FormFooter Footer
        {
            get
            {
                return base.footer;
            }
        }
        protected override FormIntroduction Intro
        {
            get
            {
                return base.intro;
            }
        }
        protected override FormSubmit Submit
        {
            get
            {
                return base.submit;
            }
        }
        protected override Sitecore.Form.Web.UI.Controls.SubmitSummary SubmitSummary
        {
            get
            {
                return base.submitSummary;
            }
        }
        protected override FormTitle Title
        {
            get
            {
                return base.title;
            }
        }

        public new event EventHandler<EventArgs> SucceedValidation;
        public new event EventHandler<EventArgs> SucceedSubmit;
        public new event EventHandler<EventArgs> FailedSubmit;
  }
}
