using System;
using System.Collections.Generic;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.ExM.Framework.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Core.Personalization;
using Sitecore.Modules.EmailCampaign.Exceptions;
using Sitecore.Modules.EmailCampaign.Messages;
using Sitecore.Modules.EmailCampaign.RecipientCollections;
using Sitecore.Modules.EmailCampaign.Recipients;
using Sitecore.Text;

namespace Sitecore.Support.Modules.EmailCampaign.Messages
{
  public abstract class MessageItem : Sitecore.Modules.EmailCampaign.Messages.MessageItem
  {
    private List<MediaItem> attachments;
    private string body;
    private ID campaignId;
    private Dictionary<string, object> customPersonTokens = new Dictionary<string, object>();
    private bool? emulation;
    private string fromAddress;
    private Item innerItem;
    private ManagerRoot managerRoot;
    private Guid? messageId;
    private PersonalizationManager personalizationManager;
    private Recipient personalizationRecipient;
    private ID planId;
    private IRecipientManager recipientManager;
    private string subject;
    private Language targetLanguage;
    private int? testSize;
    private float? testSizePercent;
    private string to;
    private bool? usePreferredLanguage;
    private ListString warnings = new ListString();



    public abstract override object Clone();
    protected virtual void CloneFields(MessageItem newMessage)
    {
      newMessage.customPersonTokens = customPersonTokens;
      newMessage.warnings = warnings;
      newMessage.body = body;
      newMessage.fromAddress = fromAddress;
      newMessage.subject = subject;
      newMessage.to = to;
      newMessage.attachments = attachments;
      newMessage.innerItem = innerItem;
      newMessage.personalizationRecipient = personalizationRecipient;
      newMessage.personalizationManager = personalizationManager;
      newMessage.targetLanguage = targetLanguage;
      newMessage.usePreferredLanguage = usePreferredLanguage;
      newMessage.campaignId = campaignId;
      newMessage.managerRoot = managerRoot;
    }

    protected static Item Create(string name, Item destination, string messageTemplateId)
    {
      Assert.ArgumentNotNull(destination, "destination");
      Assert.ArgumentNotNullOrEmpty(name, "name");
      Assert.ArgumentNotNullOrEmpty(messageTemplateId, "messageTemplateId");
      return ItemUtilExt.AddItemFromTemplate(name, messageTemplateId, destination);
    }

    public virtual string GetContactAddress(Contact contact)
    {
      if (!Util.IsValidEmail(contact.Profile.Email))
      {
        throw new NonCriticalException("'{0}' is not a valid email address.", contact.Profile.Email);
      }
      return contact.Profile.Email;
    }

    public virtual string GetMessageBody() =>
        GetMessageBody(false);

    public abstract override string GetMessageBody(bool preview);
    public virtual void InitializeCommonFields()
    {
      ManagerRoot managerRoot = ManagerRoot;
      string fromAddress = FromAddress;
    }

    public abstract override QueryResult<bool> IsSubscribed(RecipientId subscriberId);
    public virtual string ReplaceTokens(string text)
    {
      if (PersonalizationManager != null)
      {
        text = PersonalizationManager.ModifyText(text);
      }
      return text;
    }

    protected virtual void ResetLocalizableFields()
    {
      body = null;
      fromAddress = null;
      subject = null;
      attachments = null;
    }

    private void SetInnerItemAccordingToTargetLanguage()
    {
      InnerItem = InnerItem.Database.GetItem(InnerItem.ID, TargetLanguage) ?? InnerItem;
      managerRoot = null;
    }

    private bool VerifyLanguage(Item item)
    {
      if ((TargetLanguage != null) && !(TargetLanguage == item.Language))
      {
        return false;
      }
      return true;
    }

    public virtual List<MediaItem> Attachments
    {
      get
      {
        return
          (attachments ?? (attachments = Source.Attachments));
      }
      set
      {
        attachments = value;
      }
    }

    public virtual string Body
    {
      get
      {
        return
  body;
      }

      set
      {
        body = value;
      }
    }

    public virtual ID CampaignId
    {
      get
      {
        if ((campaignId == (ID)null) || (campaignId == new ID(Guid.Empty)))
        {
          campaignId = Source.CampaignId;
        }
        return campaignId;
      }
      set
      {
        campaignId = value;
      }
    }

    public string CampaignPosition =>
        Source.CampaignPosition;

    public Dictionary<string, object> CustomPersonTokens =>
        customPersonTokens;

    public string Description =>
        Source.Description;

    public virtual MessageDisplayMode DisplayMode { get; set; }

    public string DisplayName =>
        Source.DisplayName;

    public virtual string EmailPreview
    {
      get
      {
        return
          InnerItem["{350382B2-284C-4DCB-ADD7-77ED5490B4FA}"];
      }
      set
      {
        InnerItem.Editing.BeginEdit();
        InnerItem["{350382B2-284C-4DCB-ADD7-77ED5490B4FA}"] = value ?? string.Empty;
        InnerItem.Editing.EndEdit();
      }
    }

    public bool Emulation
    {
      get
      {
        if (!emulation.HasValue)
        {
          emulation = Source.Emulation;
        }
        return emulation.GetValueOrDefault(false);
      }
    }

    public virtual bool EnableNotifications =>
        Source.EnableNotifications;

    public virtual DateTime EndTime =>
        Source.EndTime;

    public virtual string FromAddress
    {
      get
      {
        return
          (fromAddress ?? (fromAddress = Source.FromAddress));
      }
      set
      {
        fromAddress = value;
      }
    }

    public virtual IGuidCryptoServiceProvider GuidCryptoServiceProvider { get; set; }

    public virtual string ID =>
        InnerItem.ID.ToString();

    public Item InnerItem
    {
      get
      {
        return
          innerItem;
      }
      protected set
      {
        innerItem = value;
      }
    }

    public abstract override bool IsSubscribersIdsUncommittedRead { get; }

    public virtual ManagerRoot ManagerRoot
    {
      get
      {
        if (managerRoot == null)
        {
          managerRoot = Factory.GetManagerRootFromChildItem(InnerItem);
          if (managerRoot == null)
          {
            Logger.Instance.LogError($"The item ID: {InnerItem.ID} could not find the Email Experience Manager Root it requested.");
            throw new EmailCampaignException("The item ID: {0} could not find the Email Experience Manager Root it requested.", InnerItem.ID);
          }
        }
        return managerRoot;
      }
    }

    public virtual Guid MessageId
    {
      get
      {
        Guid? messageId = this.messageId;
        if (!messageId.HasValue)
        {
          return InnerItem.ID.ToGuid();
        }
        return messageId.GetValueOrDefault();
      }
    }

    public virtual MessageType MessageType =>
        Source.MessageType;

    public virtual string Name =>
        Source.Name;

    public virtual string NotificationAddress =>
        Source.NotificationAddress;

    protected virtual PersonalizationManager PersonalizationManager
    {
      get
      {
        if (personalizationManager == null)
        {
          personalizationManager = new PersonalizationManager();
          if (CustomPersonTokens.Count > 0)
          {
            DictionaryTokenMapper tokenMapper = new DictionaryTokenMapper();
            foreach (KeyValuePair<string, object> pair in CustomPersonTokens)
            {
              if (!string.IsNullOrWhiteSpace(pair.Key) && (pair.Value != null))
              {
                tokenMapper.BindToken(new Token(pair.Key), pair.Value);
              }
            }
            personalizationManager.AddTokenMapper(tokenMapper);
          }
          if (PersonalizationRecipient != null)
          {
            RecipientPropertyTokenMapper mapper2 = new RecipientPropertyTokenMapper(PersonalizationRecipient);
            personalizationManager.AddTokenMapper(mapper2);
          }
        }
        return personalizationManager;
      }
      set
      {
        personalizationManager = value;
      }
    }

    public virtual Recipient PersonalizationRecipient
    {
      get
      {
        return
          personalizationRecipient;
      }
      set
      {
        if (personalizationRecipient != value)
        {
          PersonalizationManager = null;
        }
        personalizationRecipient = value;
        Event.RaiseEvent("subscriber:assigned", this);
      }
    }

    public virtual ID PlanId
    {
      get
      {
        if ((planId == (ID)null) || (planId == new ID(Guid.Empty)))
        {
          planId = Source.PlanId;
        }
        return planId;
      }
      set
      {
        planId = value;
      }
    }

    public virtual IRecipientManager RecipientManager =>
        (recipientManager ?? (recipientManager = Source.RecipientManager));

    public string ShortID =>
        InnerItem.ID.ToShortID().ToString();

    public new Sitecore.Modules.EmailCampaign.Messages.MessageItemSource Source { get; set; }

    public virtual string SpamDetect
    {
      get
      {
        return
          InnerItem["{C5383CE9-B7C9-4E24-8211-B6EB628DC9AE}"];
      }
      set
      {
        InnerItem.Editing.BeginEdit();
        InnerItem["{C5383CE9-B7C9-4E24-8211-B6EB628DC9AE}"] = value ?? string.Empty;
        InnerItem.Editing.EndEdit();
      }
    }

    public virtual DateTime StartTime =>
        Source.StartTime;

    public virtual MessageState State =>
        Source.State;

    public virtual string Subject
    {
      get
      {
        return
          subject;
      }
      set
      {
        subject = value;
      }
    }

    public abstract override QueryResult<int> SubscribersCount { get; }

    public abstract override QueryResult<List<RecipientId>> SubscribersIds { get; }

    public virtual Language TargetLanguage
    {
      get
      {
        return
          (targetLanguage ?? (targetLanguage = Source.TargetLanguage));
      }
      set
      {
        targetLanguage = value;
        if (!VerifyLanguage(InnerItem))
        {
          SetInnerItemAccordingToTargetLanguage();
          ResetLocalizableFields();
        }
      }
    }

    public virtual int TestSize
    {
      get
      {
        if (!testSize.HasValue)
        {
          testSize = Source.TestSize;
        }
        return testSize.GetValueOrDefault();
      }
      set
      {
        testSize = value;
      }
    }

    public virtual float TestSizePercent
    {
      get
      {
        if (!testSizePercent.HasValue)
        {
          testSizePercent = Source.TestSizePercent;
        }
        return testSizePercent.GetValueOrDefault();
      }
      set
      {
        testSizePercent = value;
      }
    }

    public virtual string To
    {
      get
      {
        return
          to;
      }
      set
      {
        to = value;
      }
    }

    public override bool UsePreferredLanguage
    {
      get
      {
        if (!usePreferredLanguage.HasValue)
        {
          usePreferredLanguage = Source.UsePreferredLanguage;
        }
        return usePreferredLanguage.GetValueOrDefault(false);
      }
      set
      {
        usePreferredLanguage = value;
      }
    }

    public virtual ListString Warnings =>
        warnings;

    protected MessageItem(Item item) : base(item){}
  }
}