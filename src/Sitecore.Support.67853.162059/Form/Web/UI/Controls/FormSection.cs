using Sitecore.Data.Items;
using Sitecore.Form.Core.Attributes;
using Sitecore.Form.Core.Configuration;
using Sitecore.Form.Core.Controls.Html;
using Sitecore.Form.Core.Visual;
using Sitecore.Form.Web.UI.Controls;
using Sitecore.Forms.Core.Data;
using System;
using System.ComponentModel;
using System.Web.UI;
using System.Web.UI.WebControls;
using Sitecore.WFFM.Abstractions.Data;

namespace Sitecore.Support.Form.Web.UI.Controls
{
    [PersistChildren(true), ToolboxData("<div runat=\"server\"></div>")]
    internal class FormSection : WebControl
    {
        protected Div content;
        private readonly IFieldItem[] fields;
        private readonly Item section;
        private bool showLegend;

        public FormSection(Item section, IFieldItem[] fields, string validationGroup, bool fastPreview) : base(HtmlTextWriterTag.Div)
        {
            this.showLegend = true;
            this.section = section;
            this.fields = fields;
            this.ValidationGroup = validationGroup;
            this.IsFastPreview = fastPreview;
            this.IsFastPreview = false;
            this.RenderAsFieldSet = false;
            this.Information = string.Empty;
            this.ReadQueryString = false;
        }

        public FormSection(Item section, IFieldItem[] fields, bool renderAsFieldSet, string validationGroup, bool fastPreview) : this(section, fields, validationGroup, fastPreview)
        {
            this.RenderAsFieldSet = renderAsFieldSet;
            this.ReadQueryString = false;
        }

        private void AddFields(Control content)
        {
            foreach (FieldItem item in this.fields)
            {
                if ((item.Type != null) && !string.IsNullOrEmpty(item.Title))
                {
                    ControlReference child = new ControlReference(item)
                    {
                        IsFastPreview = this.IsFastPreview,
                        ValidationGroup = this.ValidationGroup,
                        ReadQueryString = this.ReadQueryString,
                        DisableWebEditing = this.DisableWebEditing,
                        RenderingParameters = this.RenderingParameters
                    };
                    content.Controls.Add(child);
                }
            }
        }

        protected override void OnInit(EventArgs e)
        {
            HtmlBaseControl control;
            base.OnInit(e);
            // Support fix #162059
            if (String.IsNullOrEmpty(this.CssClass))
                this.CssClass = "scfSectionBorder";
            if (this.RenderAsFieldSet)
            {
                Fieldset fieldset = new Fieldset
                {
                    Class = "scfSectionBorderAsFieldSet"
                };
                control = fieldset;
            }
            else
            {
                if (string.IsNullOrEmpty(this.CssClass))
                {
                    this.CssClass = "scfSectionBorder";
                }
                Div div = new Div
                {
                    Class = this.CssClass
                };
                control = div;
            }
            foreach (string str in base.Attributes.Keys)
            {
                control.Attributes[str] = base.Attributes[str];
            }
            control.Style.Value = base.Style.Value;
            this.Controls.Add(control);
            if (this.showLegend && this.RenderAsFieldSet)
            {
                string str2 = FieldRenderer.Render(this.section, Sitecore.Form.Core.Configuration.FieldIDs.FieldTitleID, this.RenderingParameters, this.DisableWebEditing);
                if (!string.IsNullOrEmpty(str2))
                {
                    Legend child = new Legend
                    {
                        Class = "scfSectionLegend",
                        Title = str2
                    };
                    control.Controls.Add(child);
                }
            }
            if (!string.IsNullOrEmpty(this.Information))
            {
                HtmlParagraph paragraph = new HtmlParagraph
                {
                    Class = "scfSectionUsefulInfo",
                    Text = this.Information
                };
                control.Controls.Add(paragraph);
            }
            Div div2 = new Div
            {
                Class = "scfSectionContent"
            };
            this.content = div2;
            control.Controls.Add(this.content);
            this.AddFields(this.content);
        }

        protected override void OnPreRender(EventArgs e)
        {
            if (this.content.Controls.FirstOrDefault(c => ((c is ControlReference) && c.HasControls())) == null)
            {
                this.Visible = false;
            }
        }

        [DefaultValue("scfSectionBorder"), VisualProperty("CSS Class:", 600), VisualFieldType(typeof(CssClassField))]
        public new string CssClass
        {
            get
            {
                return base.CssClass;
            }
            set
            {
                base.CssClass = value;
            }
        }

        public bool DisableWebEditing { get; set; }

        [Localize, VisualProperty("Help:", 500), VisualFieldType(typeof(TextAreaField)), Browsable(false)]
        public string Information { get; set; }

        [Browsable(false)]
        public bool IsFastPreview { get; set; }

        [Browsable(false)]
        public bool ReadQueryString { get; set; }

        [Browsable(false)]
        public bool RenderAsFieldSet { get; set; }

        public string RenderingParameters { get; set; }

        [DefaultValue("Yes"), VisualProperty("Show Title:", 400), Browsable(false), VisualFieldType(typeof(BooleanField))]
        public string ShowLegend
        {
            get
            {
                return this.showLegend.ToString();
            }
            set
            {
                this.showLegend = value == "Yes";
            }
        }

        [Browsable(false)]
        private string ValidationGroup
        {
            get
            {
                return (this.ViewState["summary"] as string);
            }
            set
            {
                this.ViewState["summary"] = value;
            }
        }
    }
}