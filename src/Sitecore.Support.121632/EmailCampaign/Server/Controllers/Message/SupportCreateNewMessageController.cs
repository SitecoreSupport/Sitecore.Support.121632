using System.Text;
using System.Web;
using System.Web.Http;
using Sitecore.Diagnostics;
using Sitecore.EmailCampaign.Server.Contexts;
using Sitecore.EmailCampaign.Server.Helpers;
using Sitecore.EmailCampaign.Server.Responses;
using Sitecore.Modules.EmailCampaign.Core;
using Sitecore.Services.Core;
using Sitecore.Services.Infrastructure.Web.Http;
using Sitecore.Support.EmailCampaign.Server.Filters;
using MessageHelper = Sitecore.Support.EmailCampaign.Server.Helpers.MessageHelper;

namespace Sitecore.Support.EmailCampaign.Server.Controllers.Message
{
  [ServicesController("EXM.SupportCreateNewMessage"), SitecoreAuthorize(@"sitecore\ECM Advanced Users", @"sitecore\ECM Users")]
  public class SupportCreateNewMessageController : ServicesApiController
  {
    private IMessageHelper messageHelper;

    public SupportCreateNewMessageController() : this(new MessageHelper())
    {
    }

    public SupportCreateNewMessageController(IMessageHelper messageHelper)
    {
      Assert.ArgumentNotNull(messageHelper, "messageHelper");
      this.messageHelper = messageHelper;
    }

    [ActionName("DefaultAction")]
    public Response Create(NewMessageContext data)
    {
      Assert.ArgumentNotNull(data, "data");
      Assert.IsNotNull(data.MessageName, "Could not get new message name from the context for data:{0}", data);
      Assert.IsNotNull(data.ManagerRootId, "Could not get mananger root Id from the context for data:{0}", data);
      Assert.IsNotNull(data.MessageTemplateId, "Could not get message template Id from the context for data:{0}", data);
      Assert.IsNotNull(data.MessageTypeTemplateId, "Could not get message type template Id from the context for data:{0}", data);

      string str = messageHelper.CreateNewMessage(data.ManagerRootId, data.MessageTemplateId, HttpUtility.UrlDecode(data.MessageName, Encoding.Default), data.MessageTypeTemplateId, data.LayoutId);
      if (str != null)
      {
        return new StringResponse { Value = str };
      }
      return new StringResponse
      {
        Error = true,
        ErrorMessage = EcmTexts.Localize("Failed to create the message.")
      };
    }
  }
}
