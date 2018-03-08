using System;
using System.Web.Http;
using System.Web.Http.Controllers;
using Sitecore.Diagnostics;
using Sitecore.Security.Accounts;

namespace Sitecore.Support.EmailCampaign.Server.Filters
{
  [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
  internal sealed class SitecoreAuthorizeAttribute : AuthorizeAttribute
  {
    private static readonly ITicketManager TicketManager = new TicketManagerWrapper();

    public SitecoreAuthorizeAttribute(params string[] roles)
    {
      Roles = string.Join(",", roles);
    }

    protected override bool IsAuthorized(HttpActionContext actionContext)
    {
      Assert.ArgumentNotNull(actionContext, "actionContext");
      var flag = base.IsAuthorized(actionContext) && !AdminsOnly;
      var principal = actionContext.ControllerContext.RequestContext.Principal as User;
      var flag2 = (principal != null) && principal.IsAdministrator;
      return ((flag || flag2) && TicketManager.IsCurrentTicketValid());
    }

    public bool AdminsOnly { get; set; }

    internal interface ITicketManager
    {
      bool IsCurrentTicketValid();
    }

    private class TicketManagerWrapper : ITicketManager
    {
      public bool IsCurrentTicketValid() => true;
    }
  }
}
