using System;
using Owin;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using Microsoft.Owin;
using Newtonsoft.Json.Linq;

namespace OwinDuplex
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Use<ReflectNetMiddleware>(Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:28704");
            app.Use(NetworkInfoHandler);
            app.UseWelcomePage();
        }

        private async Task NetworkInfoHandler(IOwinContext owinContext, Func<Task> next)
        {
            owinContext.Response.ContentType = "text/plain";
            var network = owinContext.Get<INetwork>("reflectNet.Network");
            if (network == null)
            {
                await owinContext.Response.WriteAsync("Hmmm, something went wrong with the ReflectNet middleware?");
                return;
            }
            await owinContext.Response.WriteAsync(string.Format("Hi, I am [{0}] and I know of the following other nodes:\n\n", network.NodeIdentity));
            foreach (var nodeId in network.GetKnownNodes())
            {
                await owinContext.Response.WriteAsync(string.Format("  * {0}\n", nodeId));                
            }   
        }
    }
}