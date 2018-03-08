using System.Web;
using Sitecore.Data;
using Sitecore.EmailCampaign.Server.Helpers;
using Sitecore.Globalization;
using Sitecore.Modules.EmailCampaign;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Modules.EmailCampaign.Messages;
using MessageItemSource = Sitecore.Support.Modules.EmailCampaign.Messages.MessageItemSource;

namespace Sitecore.Support.EmailCampaign.Server.Helpers
{
  public class MessageHelper : IMessageHelper
  {
    public string CreateNewMessage(string managerRootId, string messageTemplateId, string messageName, string messageTypeTemplateId, string layoutId)
    {
      var managerRootFromId = Factory.GetManagerRootFromID(managerRootId);

      if (managerRootFromId == null) return null;

      MessageItem item2;
      if (string.IsNullOrEmpty(messageTypeTemplateId))
      {
        return null;
      }

      var item = managerRootFromId.InnerItem.Axes.SelectSingleItem($"./descendant::*[@@tid='{new ID(messageTypeTemplateId)}']");

      if (item == null)
      {
        return null;
      }

      Language result = null;

      if ((!string.IsNullOrEmpty(Context.User.Profile.ContentLanguage) && Language.TryParse(Context.User.Profile.ContentLanguage, out result)) && !Util.GetContentDb().GetLanguages().Contains(result))
      {
        result = null;
      }
      if (string.IsNullOrEmpty(layoutId))
      {
        item2 = MessageItemSource.Create(HttpUtility.HtmlEncode(messageName), messageTemplateId, item.ID.ToString(), result);
      }
      else
      {
        var item3 = new ItemUtilExt().GetItem(layoutId);

        if (item3 == null)
        {
          return null;
        }

        item2 = ABTestMessageSource.CreateAbTestMessage(messageName, item3.ID, item.ID, messageTemplateId, result);
      }

      return item2?.ID;
    }
  }
}
