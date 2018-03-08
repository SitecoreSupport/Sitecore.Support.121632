using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.StringExtensions;
using Sitecore.Text;
using MessageRelationsCorrector = Sitecore.Support.Modules.EmailCampaign.Core.MessageRelationsCorrector;

namespace Sitecore.Support.Modules.EmailCampaign.Messages
{
  public class MessageItemSource
  {
    private readonly MessageItem message;

    public MessageItemSource(MessageItem message)
    {
      this.message = message;
    }

    private string BoolToString(bool value)
    {
      if (!value)
      {
        return string.Empty;
      }
      return "1";
    }

    protected virtual bool ChangeItemsProtection(bool protect)
    {
      message.InnerItem.Editing.BeginEdit();
      message.InnerItem.Appearance.ReadOnly = protect;
      message.InnerItem.Editing.EndEdit();
      return true;
    }

    public virtual void ClearTestSize()
    {
      SetField("{2DF7DE6E-A412-4A98-B8C4-B7AB7032634A}", string.Empty);
    }

    public static Sitecore.Modules.EmailCampaign.Messages.MessageItem Create(string templateId, string typeId, Language language = null) =>
        Create(string.Empty, templateId, typeId, language);

    public static Sitecore.Modules.EmailCampaign.Messages.MessageItem Create(string name, string templateId, string typeId, Language language = null)
    {
      Item type = new ItemUtilExt().GetItem(typeId);
      Item destination = GetDestination(type);
      return Create(name, templateId, type, destination, language);
    }

    public static Sitecore.Modules.EmailCampaign.Messages.MessageItem Create(string name, string templateId, Item typeItem, Item destination, Language language = null)
    {
      Item item2;
      Assert.ArgumentNotNull(destination, "destination");
      Item item = new ItemUtilExt().GetItem(templateId);
      if (item.TemplateID == TemplateIDs.CommandMaster)
      {
        return null;
      }
      string str = name;
      if (!string.IsNullOrEmpty(str))
      {
        str = ItemUtilExt.ProposeValidItemName(str);
        if (!string.IsNullOrEmpty(ItemUtil.GetItemNameError(str)))
        {
          str = item.Name;
        }
      }
      else
      {
        str = ItemUtilExt.ProposeValidItemName(item.Name);
      }
      if (item.TemplateID == TemplateIDs.BranchTemplate)
      {
        BranchItem branch = item;
        string str2 = branch["{0934BEC4-325A-427B-95BB-46F78CEA9591}"];
        if (!string.IsNullOrEmpty(str2))
        {
          Item item4 = branch.InnerItem.Children.First();
          if (language != null)
          {
            item4 = item4.Database.GetItem(item4.ID, language);
          }
          item2 = item4.CopyTo(destination, str);
          item2.Editing.BeginEdit();
          item2.BranchId = branch.ID;
          item2.Editing.EndEdit();
        }
        else
        {
          if (language != null)
          {
            destination = destination.Database.GetItem(destination.ID, language);
          }
          item2 = destination.Add(str, branch);
        }
      }
      else
      {
        TemplateItem template = item;
        if (language != null)
        {
          destination = destination.Database.GetItem(destination.ID, language);
        }
        item2 = destination.Add(str, template);
      }
      if (item2 == null)
      {
        return null;
      }
      Sitecore.Modules.EmailCampaign.Messages.MessageItem messageItem = Factory.Instance.GetMessageItem(item2);
      if (messageItem == null)
      {
        item2.Delete();
        throw new EmailCampaignException("The selected template is not of message type.");
      }
      MessageTypeItem type = new MessageTypeItem(typeItem);
      messageItem.Source.DisplayName = string.IsNullOrEmpty(name) ? item.DisplayName : name;

      var sourceType = messageItem.Source.GetType();
      var mi = sourceType.GetMethod("SetDefaults", BindingFlags.Instance|BindingFlags.NonPublic);
      mi.Invoke(messageItem.Source, new object[] {type});

      //messageItem.Source.SetDefaults(type);
      if (item2.HasChildren)
      {
        using (new EditContext(messageItem.InnerItem))
        {
          messageItem.InnerItem[FieldIDs.LayoutField] = item2.Children.First(x => (x.TemplateID != TemplateIDs.Folder)).Fields[FieldIDs.LayoutField].Value;
        }
      }
      MessageRelationsCorrector.SetMessageType(messageItem, type);
      return messageItem;
    }

    private static Item GetDestination(Item type)
    {
      Item destinationRoot = Factory.GetManagerRootFromChildItem(type).InnerItem.Axes.SelectSingleItem("Messages");
      if (destinationRoot == null)
      {
        return null;
      }
      return new ItemUtilExt().GetDestinationFolderItem(destinationRoot);
    }

    public bool LockRelatedItems() =>
        ChangeItemsProtection(true);

    private string MessageStateToString(MessageState state)
    {
      MessageState state2 = state;
      if (state2 != MessageState.DispatchScheduled)
      {
        if (state2 == MessageState.ActivationScheduled)
        {
          return "Activation Scheduled";
        }
        return state.ToString();
      }
      return "Dispatch Scheduled";
    }

    private string MessageTypeToString(MessageType type)
    {
      switch (type)
      {
        case MessageType.OneTime:
          return "{C2786C9C-B267-4DD8-B8FB-4452A23F9969}";

        case MessageType.Subscription:
          return "{880439AC-739A-4F06-A3E3-8CFDB820123E}";

        case MessageType.Triggered:
          return "{B2F77E26-C5AC-4A6B-BBB3-F290100EB3D8}";
      }
      return type.ToString();
    }

    public bool ReleaseRelatedItems() =>
        ChangeItemsProtection(false);

    protected virtual void SetDefaults(MessageTypeItem type)
    {
      if ((type.Type == MessageType.Subscription) && !string.IsNullOrEmpty(type.FromAddress))
      {
        FromAddress = type.FromAddress;
      }
      else
      {
        FromAddress = message.ManagerRoot.Settings.FromAddress;
      }
    }

    protected void SetField(string key, string value)
    {
      Item innerItem = message.InnerItem;
      innerItem.Editing.BeginEdit();
      innerItem[key] = value ?? string.Empty;
      innerItem.Editing.EndEdit();
    }

    public void SetValueIfEmptyOrDefault(string fieldKey, string defaultValue, string value)
    {
      Field field = message.InnerItem.Fields[fieldKey];
      if (!field.HasValue || (field.Value == defaultValue))
      {
        SetField(fieldKey, value);
      }
    }

    private MessageState StringToMessageState(string state) =>
        ((MessageState)Enum.Parse(typeof(MessageState), state.Replace(" ", string.Empty), true));

    private MessageType StringToMessageType(string messageType)
    {
      MessageType type;
      switch (messageType)
      {
        case "{C2786C9C-B267-4DD8-B8FB-4452A23F9969}":
          return MessageType.OneTime;

        case "{B2F77E26-C5AC-4A6B-BBB3-F290100EB3D8}":
          return MessageType.Triggered;

        case "{880439AC-739A-4F06-A3E3-8CFDB820123E}":
          return MessageType.Subscription;
      }
      if (!Enum.TryParse(messageType, true, out type))
      {
        return MessageType.Undefined;
      }
      return type;
    }

    public virtual List<MediaItem> Attachments
    {
      get
      {
        List<MediaItem> list = new List<MediaItem>();
        ListString str = new ListString(message.InnerItem["Attachments"]);
        foreach (string str2 in str)
        {
          MediaItem item = new ItemUtilExt().GetItem(str2, message.InnerItem.Language, true);
          if (item != null)
          {
            list.Add(item);
          }
        }
        return list;
      }
      set
      {
        string str = (value != null) ? new ListString((from e in value select e.ID.ToString()).ToList()).ToString() : string.Empty;
        SetField("Attachments", str);
        message.Attachments = value;
      }
    }

    public string CampaignGroup
    {
      get
      {
        return message.InnerItem["{FADE34D8-C101-420D-9620-E4DEB659C36B}"];
      }
      set
      {
        SetField("{FADE34D8-C101-420D-9620-E4DEB659C36B}", value);
      }
    }

    public ID CampaignId
    {
      get
      {
        ID id;
        if (!ID.TryParse(message.InnerItem["{78F0B1F8-FDF4-4C1C-BEC6-1AC68A210A52}"], out id))
        {
          return ID.Null;
        }
        return id;
      }
      set
      {
        SetField("{78F0B1F8-FDF4-4C1C-BEC6-1AC68A210A52}", (value != (ID)null) ? value.ToString() : string.Empty);
        message.CampaignId = value;
      }
    }

    public string CampaignPosition
    {
      get
      {
        return message.InnerItem["{2BBF5276-ED11-4CD0-946F-66678B524457}"];
      }
      set
      {
        SetField("{2BBF5276-ED11-4CD0-946F-66678B524457}", value);
      }
    }

    public string Description
    {
      get
      {
        return message.InnerItem["__Long Description"];
      }
      set
      {
        SetField("__Long Description", value);
      }
    }

    public string DisplayName
    {
      get
      {
        return
          message.InnerItem.DisplayName;
      }
      set
      {
        Item innerItem = message.InnerItem;
        innerItem.Editing.BeginEdit();
        innerItem.Appearance.DisplayName = new ItemUtilExt().SanitizeName(value) ?? string.Empty;
        innerItem.Editing.EndEdit();
      }
    }

    public bool Emulation
    {
      get
      {
        return
          MainUtil.GetBool(message.InnerItem["{ED7995DD-FCDC-4CD2-A660-48201F402489}"], false);
      }
      set
      {
        SetField("{ED7995DD-FCDC-4CD2-A660-48201F402489}", BoolToString(value));
      }
    }

    public bool EnableNotifications
    {
      get
      {
        return
          MainUtil.GetBool(message.InnerItem["{EE1D1DC9-0C65-4E1C-A1C1-06D5EAC23A7C}"], false);
      }
      set
      {
        SetField("{EE1D1DC9-0C65-4E1C-A1C1-06D5EAC23A7C}", BoolToString(value));
      }
    }

    public DateTime EndTime
    {
      get
      {
        string str = message.InnerItem["{AC436B75-B307-4409-8479-E299ACE64859}"];
        if (string.IsNullOrEmpty(str))
        {
          return DateTime.MaxValue;
        }
        return DateUtil.IsoDateToDateTime(str, DateTime.MaxValue);
      }
      set
      {
        SetField("{AC436B75-B307-4409-8479-E299ACE64859}", (value != DateTime.MaxValue) ? DateUtil.ToIsoDate(value) : string.Empty);
      }
    }

    public string FromAddress
    {
      get
      {
        return
          message.InnerItem["{0D844C9C-BD6C-49B8-9475-947DA791F268}"];
      }
      set
      {
        SetField("{0D844C9C-BD6C-49B8-9475-947DA791F268}", value);
      }
    }

    public string FromName
    {
      get
      {
        return
          message.InnerItem["{8436A90C-85C0-4F16-8373-393C4C0A82C6}"];
      }
      set
      {
        SetField("{8436A90C-85C0-4F16-8373-393C4C0A82C6}", value);
      }
    }

    public MessageType MessageType
    {
      get
      {
        string messageType = message.InnerItem["{0934BEC4-325A-427B-95BB-46F78CEA9591}"];
        return StringToMessageType(messageType);
      }
      set
      {
        SetField("{0934BEC4-325A-427B-95BB-46F78CEA9591}", MessageTypeToString(value));
      }
    }

    public string Name
    {
      get
      {
        return
          message.InnerItem.Name;
      }
      set
      {
        string name = ItemUtilExt.ProposeValidItemName(value);
        if (string.IsNullOrEmpty(ItemUtil.GetItemNameError(name)))
        {
          message.InnerItem.Editing.BeginEdit();
          message.InnerItem.Name = name;
          message.InnerItem.Editing.EndEdit();
        }
      }
    }

    public string NotificationAddress
    {
      get
      {
        return
          message.InnerItem["{0487113D-4DE1-4CA7-A5D0-9FADFD0EF187}"];
      }
      set
      {
        SetField("{0487113D-4DE1-4CA7-A5D0-9FADFD0EF187}", value);
      }
    }

    public ID PlanId
    {
      get
      {
        ID id;
        if (!ID.TryParse(message.InnerItem["{ED5A9033-96ED-41F8-9731-7499C55ECBFA}"], out id))
        {
          return ID.Null;
        }
        return id;
      }
      set
      {
        SetField("{ED5A9033-96ED-41F8-9731-7499C55ECBFA}", (value != (ID)null) ? value.ToString() : string.Empty);
      }
    }

    public virtual IRecipientManager RecipientManager =>
        Factory.Instance.GetRecipientManager(message.InnerItem);

    public string ReplyTo
    {
      get
      {
        return
          message.InnerItem["{CED0C05F-D1CB-46E9-875B-E2C01EEC85BC}"];
      }
      set
      {
        SetField("{CED0C05F-D1CB-46E9-875B-E2C01EEC85BC}", value);
      }
    }

    public DateTime StartTime
    {
      get
      {
        string str = message.InnerItem["{6EC7A9E6-9FD5-4892-BBF6-11E76E0E5DB8}"];
        if (string.IsNullOrEmpty(str))
        {
          return DateTime.MinValue;
        }
        return DateUtil.IsoDateToDateTime(str, DateTime.MinValue);
      }
      set
      {
        SetField("{6EC7A9E6-9FD5-4892-BBF6-11E76E0E5DB8}", (value != DateTime.MinValue) ? DateUtil.ToIsoDate(value) : string.Empty);
      }
    }

    public MessageState State
    {
      get
      {
        string str = message.InnerItem["{AF0FE26A-A78B-491D-885C-03D95BC17342}"];
        if (!string.IsNullOrEmpty(str))
        {
          return StringToMessageState(str);
        }
        return MessageState.Draft;
      }
      set
      {
        bool flag = MessageType == MessageType.Triggered;
        bool flag2 = ((value == MessageState.DispatchScheduled) || (value == MessageState.Sending)) || (value == MessageState.Sent);
        bool flag3 = ((value == MessageState.Inactive) || (value == MessageState.ActivationScheduled)) || (value == MessageState.Active);
        if (((flag && flag2) || (!flag && flag3)) || (MessageType == MessageType.Undefined))
        {
          throw new EmailCampaignException("An {0} message cannot be in the {1} state. Message ID: {2}".FormatWith(MessageType, value, message.ID));
        }
        SetField("{AF0FE26A-A78B-491D-885C-03D95BC17342}", MessageStateToString(value));
      }
    }

    public virtual Language TargetLanguage
    {
      get
      {
        Language language = null;
        string str = message.InnerItem["{5E5AF531-D68F-47FC-AEAA-C79D6320D34C}"];
        if (!string.IsNullOrEmpty(str))
        {
          Item item = Util.GetContentDb().GetItem(ID.Parse(str));
          if (item != null)
          {
            language = Language.Parse(item.Name);
          }
        }
        return (language ?? message.InnerItem.Language);
      }
      set
      {
        ID languageItemId = LanguageManager.GetLanguageItemId(value, Util.GetContentDb());
        if (!ID.IsNullOrEmpty(languageItemId))
        {
          SetField("{5E5AF531-D68F-47FC-AEAA-C79D6320D34C}", languageItemId.ToString());
        }
      }
    }

    public int TestSize
    {
      get
      {
        ListString str = new ListString(message.InnerItem["{2DF7DE6E-A412-4A98-B8C4-B7AB7032634A}"]);
        if (str.Count <= 0)
        {
          return 0;
        }
        return MainUtil.GetInt(str[0], 0);
      }
      set
      {
        SetField("{2DF7DE6E-A412-4A98-B8C4-B7AB7032634A}", $"{value.ToString(CultureInfo.InvariantCulture)}|{TestSizePercent}");
      }
    }

    public float TestSizePercent
    {
      get
      {
        ListString str = new ListString(message.InnerItem["{2DF7DE6E-A412-4A98-B8C4-B7AB7032634A}"]);
        if (str.Count <= 1)
        {
          return 0f;
        }
        return MainUtil.GetFloat(str[1], 0f);
      }
      set
      {
        SetField("{2DF7DE6E-A412-4A98-B8C4-B7AB7032634A}", $"{TestSize}|{value.ToString(CultureInfo.InvariantCulture)}");
      }
    }

    public virtual bool UsePreferredLanguage
    {
      get
      {
        return
          MainUtil.GetBool(message.InnerItem["{BD33CEBA-2450-477A-B29B-32C450F9D4D8}"], false);
      }
      set
      {
        SetField("{BD33CEBA-2450-477A-B29B-32C450F9D4D8}", BoolToString(value));
      }
    }
  }
}
