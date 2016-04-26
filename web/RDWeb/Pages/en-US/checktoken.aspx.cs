using System;
using System.Web;
using System.Configuration;

using RADAR;

public partial class CheckToken : System.Web.UI.Page
{
    private readonly string _radiusServer = ConfigurationManager.AppSettings["RadiusServer"];
    private readonly string _radiusSharedSecret = ConfigurationManager.AppSettings["RadiusSecret"];

    readonly RADIUSClient _radiusClient;

    public CheckToken()
    {
        _radiusClient = new RADIUSClient(_radiusServer, 1812, _radiusSharedSecret);
    }

    // Check validity of token (radius session id) by authenticating against 
    // the RADIUS server
    //
    // Called when clicking on applications
    // 
    // Returns 401 if not valid
    protected void Page_Load(object sender, EventArgs e)
    {
        string username = (string)Session["DomainUserName"];
        HttpCookie tokenCookie = Request.Cookies["RadiusSessionId"];

        // This must not be cached - we rely on this page being called on every application
        // start attempt in order to open the launch window.
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetMaxAge(TimeSpan.Zero);

        if (tokenCookie == null)
        {
            throw new HttpException(401, "Token required");
        }
        string token = tokenCookie.Value;

        VendorSpecificAttribute vsa = new VendorSpecificAttribute(VendorSpecificType.Generic, "LAUNCH");
        RADIUSAttributes atts = new RADIUSAttributes();
        vsa.SetRADIUSAttribute(ref atts);

        try
        {
            RADIUSPacket response = _radiusClient.Authenticate(username, token, atts);
            if (response.Code == RadiusPacketCode.AccessAccept)
            {
                Response.Write("Ready to launch application. Granted access!");
            }
            else
            {
                throw new HttpException(401, "Token is no longer valid!");
            }
        }
        catch (Exception ex)
        {
            throw new HttpException(500, "Exception! failure. " + ex.Message);
        }
    }
}