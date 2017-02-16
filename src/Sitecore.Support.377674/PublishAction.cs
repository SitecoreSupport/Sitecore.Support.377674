using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.SecurityModel;
using Sitecore.Web;
using Sitecore.Workflows.Simple;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Sitecore.Support.Workflows.Simple
{
	/// <summary>
	/// Represents a publish action.
	/// </summary>
	public class PublishAction
	{
		/// <summary>
		/// Runs the processor.
		/// </summary>
		/// <param name="args">
		/// The arguments.
		/// </param>
		public void Process(WorkflowPipelineArgs args)
		{
			Item dataItem = args.DataItem;
			Item innerItem = args.ProcessorItem.InnerItem;
			System.Collections.Specialized.NameValueCollection parameters = WebUtil.ParseUrlParameters(innerItem["parameters"]);
			bool deep = this.GetDeep(parameters, innerItem);
			bool related = this.GetRelated(parameters, innerItem);
			Database[] array = this.GetTargets(parameters, innerItem, dataItem).ToArray<Database>();
			Language[] array2 = this.GetLanguages(parameters, innerItem, dataItem).ToArray<Language>();
			bool compareRevisions = this.IsCompareRevision(parameters, innerItem);
			if (Settings.Publishing.Enabled && array.Any<Database>() && array2.Any<Language>())
			{
				PublishManager.PublishItem(dataItem, array, array2, deep, compareRevisions, related);
			}
		}

		/// <summary>
		/// Determines if the action is recursive.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <returns>
		/// <c>true</c> if action is recursive; otherwise <c>false</c>
		/// </returns>
		private bool GetDeep(System.Collections.Specialized.NameValueCollection parameters, Item actionItem)
		{
			return this.GetStringValue("deep", parameters, actionItem) == "1";
		}

		/// <summary>
		/// Determines whether to comparerevision during publish item
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <returns>
		/// <c>true</c> if action is comparerevision; otherwise <c>false</c>
		/// </returns>
		private bool IsCompareRevision(System.Collections.Specialized.NameValueCollection parameters, Item actionItem)
		{
			return this.GetStringValue("smart", parameters, actionItem) == "1";
		}

		/// <summary>
		/// Determines if related items should be published.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <returns>
		/// <c>true</c> if related items should be published; otherwise <c>false</c>.
		/// </returns>
		private bool GetRelated(System.Collections.Specialized.NameValueCollection parameters, Item actionItem)
		{
			return this.GetStringValue("related", parameters, actionItem) == "1";
		}

		/// <summary>
		/// Gets the targets.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <param name="dataItem">The data item.</param>
		/// <returns>
		/// The targets.
		/// </returns>
		private System.Collections.Generic.IEnumerable<Database> GetTargets(System.Collections.Specialized.NameValueCollection parameters, Item actionItem, Item dataItem)
		{
			using (new SecurityDisabler())
			{
				System.Collections.Generic.IEnumerable<string> enumerable = this.GetEnumerableValue("targets", parameters, actionItem);
				if (!enumerable.Any<string>())
				{
					Item item = dataItem.Database.Items["/sitecore/system/publishing targets"];
					if (item != null)
					{
						enumerable = from child in item.Children
						select child["Target database"] into dbName
						where !string.IsNullOrEmpty(dbName)
						select dbName;
					}
				}
				foreach (string current in enumerable)
				{
					Database database = Factory.GetDatabase(current, false);
					if (database != null)
					{
						yield return database;
					}
					else
					{
						Log.Warn("Unknown database in PublishAction: " + current, this);
					}
				}
			}
			yield break;
		}

		/// <summary>
		/// Gets the languages.
		/// </summary>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <param name="dataItem">The data item.</param>
		/// <returns>An enumerable of discovered languages.</returns>
		private System.Collections.Generic.IEnumerable<Language> GetLanguages(System.Collections.Specialized.NameValueCollection parameters, Item actionItem, Item dataItem)
		{
			using (new SecurityDisabler())
			{
				System.Collections.Generic.IEnumerable<string> enumerable = Enumerable.Empty<string>();
				bool flag = this.GetStringValue("alllanguages", parameters, dataItem) == "1";
				if (flag)
				{
					Item item = dataItem.Database.Items["/sitecore/system/languages"];
					if (item != null)
					{
						enumerable = from child in item.Children
						where child.TemplateID == TemplateIDs.Language
						select child.Name;
					}
				}
				else
				{
					enumerable = this.GetEnumerableValue("languages", parameters, actionItem);
					string stringValue = this.GetStringValue("itemlanguage", parameters, dataItem);
					if ((stringValue == "1" || stringValue == null) && !enumerable.Contains(dataItem.Language.Name))
					{
						yield return dataItem.Language;
					}
				}
				foreach (string current in enumerable)
				{
					Language language = null;
					if (Language.TryParse(current, out language))
					{
						yield return language;
					}
					else
					{
						Log.Warn("Unknown language in PublishAction: " + current, this);
					}
				}
			}
			yield break;
		}

		/// <summary>
		/// Gets a string value.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <returns>The discovered value or null.</returns>
		private string GetStringValue(string name, System.Collections.Specialized.NameValueCollection parameters, Item actionItem)
		{
			string text = actionItem[name];
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			text = parameters[name];
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return null;
		}

		/// <summary>
		/// Gets an enumerable value.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="parameters">The parameters.</param>
		/// <param name="actionItem">The action item.</param>
		/// <returns>An enumerable of resulting items.</returns>
		private System.Collections.Generic.IEnumerable<string> GetEnumerableValue(string name, System.Collections.Specialized.NameValueCollection parameters, Item actionItem)
		{
			string text = actionItem[name];
			if (!string.IsNullOrEmpty(text))
			{
				return text.Split(new char[]
				{
					'|'
				}, System.StringSplitOptions.RemoveEmptyEntries).AsEnumerable<string>();
			}
			text = parameters[name];
			if (!string.IsNullOrEmpty(text))
			{
				return text.Split(new char[]
				{
					','
				}, System.StringSplitOptions.RemoveEmptyEntries).AsEnumerable<string>();
			}
			return Enumerable.Empty<string>();
		}
	}
}
