using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.Layouts;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.SecurityModel;
using Sitecore.Workflows;

namespace Sitecore.Support.Modules.EmailCampaign.Core
{
  public class MessageRelationsCorrector
  {
    private static void CopyExistingPlan(MessageItem message, Item plan)
    {
      var ext = new ItemUtilExt();
      var destinationRoot = ext.GetItem("{0CD22799-4DD2-424D-A454-9D7AAB733E69}");

      if (destinationRoot == null) return;

      var destinationFolderItem = ext.GetDestinationFolderItem(destinationRoot);
      if (destinationFolderItem == null) return;

      var name = ItemUtilExt.ProposeValidItemName(message.InnerItem.DisplayName);
      var copyName = !string.IsNullOrEmpty(ItemUtil.GetItemNameError(name)) ? ItemUtilExt.ProposeValidItemName(message.InnerItem.Name) : name;
      var item = plan.CopyTo(destinationFolderItem, copyName);

      if (item == null) return;

      var item4 = message as MailMessageItem;
      var language = (item4 != null) ? item4.TargetLanguage : message.InnerItem.Language;

      foreach (var item5 in from version in item.Versions.GetVersions(true)
        where version.Language.Name != language.Name
        select version)
      {
        using (new SecurityDisabler())
        {
          item5.RecycleVersion(); 
        }
      }

      foreach (var item6 in item.Versions.GetVersions(true))
      {
        item6.Editing.BeginEdit();
        item6.Appearance.DisplayName = (message.InnerItem.Language == item6.Language) ? message.InnerItem.DisplayName : string.Empty;
        item6.Editing.EndEdit();
      }

      var workflowInfo = item.Database.DataManager.GetWorkflowInfo(item);

      if (workflowInfo != null)
      {
        item.Database.DataManager.SetWorkflowInfo(item, new WorkflowInfo(workflowInfo.WorkflowID, "{39156DC0-21C6-4F64-B641-31E85C8F5DFE}"));
      }
      message.Source.PlanId = item.ID;
    }

    public virtual void CorrectCopiedMessage(MessageItem oldMessage, MessageItem newMessage)
    {
      CreateNewPlan(newMessage, oldMessage.PlanId.ToString());
      newMessage.Source.State = MessageState.Draft;
      newMessage.Source.CampaignId = ID.Null;

      if ((!(oldMessage is WebPageMail)) || (!(newMessage is WebPageMail))) return;

      var versions = oldMessage.InnerItem.Versions.GetVersions(true);
      var itemArray2 = newMessage.InnerItem.Versions.GetVersions(true);

      for (var i = 0; i < itemArray2.Length; i++)
      {
        try
        {
          var messageItem = Factory.Instance.GetMessageItem(itemArray2[i]) as WebPageMail;
          var mail2 = Factory.Instance.GetMessageItem(versions[i]) as WebPageMail;
          if ((mail2 == null) || (messageItem == null)) continue;

          CorrectItemRelations(mail2.InnerItem, messageItem.InnerItem);
          var abnTest = CoreFactory.Instance.GetAbnTest(messageItem);

          if ((abnTest != null) && abnTest.IsTestConfigured())
          {
            abnTest.CleanTestReference();
          }

          if (((!(messageItem is ABTestMessage)) || (messageItem.TargetItem.ID != messageItem.InnerItem.ID)) || (messageItem.InnerItem.Children.Count <= 0)) continue;

          messageItem.TargetItem = messageItem.InnerItem.Children[0];
          ((WebPageMailSource)messageItem.Source).TargetItem = messageItem.InnerItem.Children[0];
        }
        catch (Exception exception)
        {
          Logger.Instance.LogError(exception);
        }
      }
    }

    public static void CorrectItemRelations(WebPageMail message)
    {
      using (new SecurityDisabler())
      {
        if ((message == null) || (message.InnerItem.Children.Count == 0)) return;
        
        var layout = LayoutDefinition.Parse(message.InnerItem.Children[0][FieldIDs.LayoutField]);

        if (layout == null) return;

        if (ReplaceItemPaths(layout))
        {
          UpdateLayout(message.InnerItem.Children[0], layout.ToXml());
        }

        UpdateMessageDefinitionLayout(message, layout);
      }
    }

    public static void CorrectItemRelations(Item oldItem, Item newItem)
    {
      Assert.ArgumentNotNull(oldItem, "oldItem");
      Assert.ArgumentNotNull(newItem, "newItem");

      var ids = new List<string>();
      var list2 = new List<string>();

      GatherChildrenIds(oldItem, ids);
      GatherChildrenIds(newItem, list2);

      if (ids.Count != list2.Count) return;

      CorrectItemRelations(newItem, ids, list2);
      ReplaceIdsInRenderings(newItem, ids, list2);
    }

    private static void CorrectItemRelations(Item item, List<string> oldIds, List<string> newIds)
    {
      foreach (Field field in item.Fields)
      {
        if ((field == null) || (field.Name.Length <= 0)) continue;

        var name = field.Name;
        var str2 = ReplaceIDs(item[name], oldIds, newIds);

        if (item[name] == str2) continue;

        item.Editing.BeginEdit();
        item[name] = str2;
        item.Editing.EndEdit();
      }
      foreach (Item item2 in item.Children)
      {
        CorrectItemRelations(item2, oldIds, newIds);
      }
    }

    public void CorrectMessageItemRelations(MessageItem message)
    {
      var plan = new ItemUtilExt().GetItem(message.PlanId);
      if (plan != null)
      {
        CopyExistingPlan(message, plan);
      }
      message.Source.CampaignId = ID.Null;

      if (message.InnerItem.Branch == null) return;

      WebPageMail mail = null;
      WebPageMail mail2 = null;

      if (message.InnerItem.TemplateID.ToString() == "{A89CF30C-EDFA-442E-8048-9234980E2176}")
      {
        mail = WebPageMail.FromItem(message.InnerItem);
        mail2 = WebPageMail.FromItem(message.InnerItem.Branch.InnerItem.Children[0]);
      }
      else if (message.InnerItem.TemplateID.ToString() == "{078D8A76-F971-4891-B422-76C0BCF9FA03}")
      {
        mail = ABTestMessage.FromItem(message.InnerItem);
        mail2 = ABTestMessage.FromItem(message.InnerItem.Branch.InnerItem.Children[0]);
      }
      if ((mail != null) && (mail2 != null))
      {
        CorrectItemRelations(mail2.InnerItem, mail.InnerItem);
      }
    }

    public static void CreateNewPlan(MessageItem message)
    {
      CreateNewPlan(message, null);
    }

    private static void CreateNewPlan(MessageItem message, string patternPlanId)
    {
      Assert.ArgumentNotNull(message, "message");

      Item plan = null;
      var ext = new ItemUtilExt();

      if (!string.IsNullOrEmpty(patternPlanId))
      {
        plan = ext.GetItem(patternPlanId, message.InnerItem.Language, true);
      }

      if (plan == null)
      {
        plan = ext.GetItem(message.PlanId, message.InnerItem.Language, true);
      }

      if (plan == null)
      {
        plan = ext.GetItem("{9B25DE0C-4F22-4086-A806-3046BC615386}", message.InnerItem.Language, true);
      }

      if (plan != null)
      {
        CopyExistingPlan(message, plan);
      }
    }

    private static void GatherChildrenIds(Item item, List<string> ids)
    {
      ids.Add(item.ID.ToString());
      foreach (Item item2 in item.Children)
      {
        GatherChildrenIds(item2, ids);
      }
    }

    private static string ReplaceIDs(string text, List<string> oldIds, List<string> newIds)
    {
      for (var i = 0; i < oldIds.Count; i++)
      {
        text = text.Replace(oldIds[i], newIds[i]);
      }
      return text;
    }

    private static void ReplaceIdsInRenderings(Item item, List<string> oldIds, List<string> newIds)
    {
      var newLayout = ReplaceIDs(item[FieldIDs.LayoutField], oldIds, newIds);

      if (item[FieldIDs.LayoutField] != newLayout)
      {
        UpdateLayout(item, newLayout);
      }

      foreach (Item item2 in item.Children)
      {
        ReplaceIdsInRenderings(item2, oldIds, newIds);
      }
    }

    private static bool ReplaceItemPaths(LayoutDefinition layout)
    {
      var flag = false;

      foreach (DeviceDefinition definition in layout.Devices)
      {
        foreach (RenderingDefinition definition2 in definition.Renderings)
        {
          if ((definition2.Datasource == null) || !definition2.Datasource.StartsWith("/")) continue;

          var item = Util.GetContentDb().GetItem(definition2.Datasource);

          if (item == null) continue;

          definition2.Datasource = item.ID.ToString();
          flag = true;
        }
      }
      return flag;
    }

    public static void SetMessageType(MessageItem message, MessageTypeItem messageType)
    {
      Assert.ArgumentNotNull(message, "message");
      Assert.ArgumentNotNull(messageType, "messageType");

      var type = messageType.Type;

      if (type == MessageType.Undefined) return;

      message.Source.MessageType = type;
      message.Source.State = MessageState.Draft;
      var campaignsPosition = messageType.CampaignsPosition;

      if (!string.IsNullOrEmpty(campaignsPosition))
      {
        message.Source.CampaignPosition = campaignsPosition;
      }

      var planId = messageType.PlanId;

      if ((message.PlanId == ID.Null) && !string.IsNullOrEmpty(planId))
      {
        CreateNewPlan(message, planId);
      }
    }

    private static void UpdateLayout(Item item, string newLayout)
    {
      item.Editing.BeginEdit();
      item[FieldIDs.LayoutField] = newLayout;
      item.Editing.EndEdit();
    }

    private static void UpdateMessageDefinitionLayout(WebPageMail message, LayoutDefinition newLayout)
    {
      var definition = LayoutDefinition.Parse(message.InnerItem[FieldIDs.LayoutField]);
      if (definition == null) return;

      foreach (DeviceDefinition definition2 in definition.Devices)
      {
        var rendering = definition2.GetRendering("{175839E3-AEF4-4830-B679-0F51B90B438E}");

        if (rendering == null) continue;
        var device = newLayout.GetDevice(definition2.ID);

        if (device == null) continue;

        for (var i = device.Renderings.Count - 1; i >= 0; i--)
        {
          var definition5 = device.Renderings[i] as RenderingDefinition;

          if (definition5 == null) continue;

          if (!string.IsNullOrEmpty(definition5.Parameters) && (definition5.Parameters.IndexOf("HideOnline=true", StringComparison.OrdinalIgnoreCase) > -1))
          {
            device.Renderings.RemoveAt(i);
          }
          else if (!string.IsNullOrEmpty(definition5.MultiVariateTest))
          {
            definition5.MultiVariateTest = string.Empty;
          }
        }

        var renderingDefinition = new RenderingDefinition
        {
          ItemID = rendering.ItemID,
          Placeholder = rendering.Placeholder,
          UniqueId = rendering.UniqueId
        };

        device.AddRendering(renderingDefinition);
      }

      UpdateLayout(message.InnerItem, newLayout.ToXml());
    }
  }
}
