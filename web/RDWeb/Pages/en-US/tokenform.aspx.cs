﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Text;
using Microsoft.TerminalServices.Publishing.Portal;
using RADAR;

public partial class SMSToken : System.Web.UI.Page
{
    String radiusServer; 
    String radiusSecret;

    String username;
    String password;

    //
    // Localizable Text
    //
    public const string L_CompanyName_Text = "Work Resources";
    public const string L_SmsToken_Text = "Enter SMS Token:";
    public const string L_LogonFailureLabel_Text = "The user name or password that you entered is not valid. Try typing it again.";
    public const string L_SubmitLabel_Text = "Submit";
    public const string L_CancelLabel_Text = "Cancel";

    //
    // Page Variables
    //
    public string sHelpSourceServer, sLocalHelp, strWorksSpaceName;
    public Uri baseUrl;

    public SMSToken() {
        radiusServer = ConfigurationManager.AppSettings["RadiusServer"];
        radiusSecret = ConfigurationManager.AppSettings["RadiusSecret"];
    }

    public void btnSignIn_Click(object sender, EventArgs e){
        String username = (string)Session["DomainUserName"];
        RADIUSAttributes atts = new RADIUSAttributes();
        RADIUSAttribute state = (RADIUSAttribute)Session["state"];
        RADIUSClient client = new RADIUSClient(radiusServer, 1812, radiusSecret);
       
        atts.Add(state);

        // Careful to use lower case username in challenge encryption to match what server does.
        String encryptedChallengeResult = CryptoHelper.SHA256(username.ToLower() + SmsToken.Text + radiusSecret);

        RADIUSPacket response = client.Authenticate(username, encryptedChallengeResult, atts);

        onRadiusResponse(response);
    }

    public void btnCancel_Click(object sender, EventArgs e){
        SafeRedirect("logoff.aspx");
    }

    void onRadiusResponse(RADIUSPacket response) {
        if (response.Code == RadiusPacketCode.AccessChallenge) {
            onRadiusChallenge(response);
        }
        else if (response.Code == RadiusPacketCode.AccessAccept) {
            onRadiusAccept(response);
        }
        else {
            onRadiusReject(response);
        }
    }

    void logoff() {
        Session["UserPass"] = null;
        Session["DomainUserName"] = null;
        SafeRedirect("logoff.aspx");
        Response.End();
    }

    void onRadiusReject(RADIUSPacket response) {
        if (response.Attributes.AttributeExists(RadiusAttributeType.ReplyMessage)){
            // Why on earth did the RD Web developer(s) use a thousand different URL parameters to logoff to indicate the error
            // message, when they could just put the message in the session
            String message = response.Attributes.GetFirstAttribute(RadiusAttributeType.ReplyMessage).ToString();
            Session["Message"] = message;
        }
        else
        {
            Session["Message"] = "Token not accepted. Try again.";
        }

        logoff();
    }

    void onRadiusChallenge(RADIUSPacket response){
        RADIUSAttribute state = response.Attributes.GetFirstAttribute(RadiusAttributeType.State);
        Session["State"] = state;
    }

    void onRadiusAccept(RADIUSPacket response){
        string sessionGuid = response.Attributes.GetFirstAttribute(RadiusAttributeType.ReplyMessage).ToString();
        Session["SESSIONGUID"] = sessionGuid;

        HttpCookie myCookie = new HttpCookie("RadiusSessionId");
        DateTime now = DateTime.Now;
        myCookie.Value = sessionGuid;
        myCookie.Expires = now.AddMinutes(480);
        Response.Cookies.Add(myCookie);

        TokenHelper.SetTwoFactorValidated(true);

        string strReturnUrlPage = "";
        if (Request.QueryString != null)
        {
            NameValueCollection objQueryString = Request.QueryString;
            if (objQueryString["ReturnUrl"] != null)
                strReturnUrlPage = objQueryString["ReturnUrl"];
        }

        SafeRedirect(strReturnUrlPage);
    }
    
    void SafeRedirect(string strRedirectUrl){
        string strRedirectSafeUrl = null;

        if (!String.IsNullOrEmpty(strRedirectUrl))
        {
            Uri baseUrl = PageContentsHelper.GetBaseUri(Context);
            Uri redirectUri = new Uri(new Uri(baseUrl, Request.FilePath), strRedirectUrl + PageContentsHelper.AppendTenantIdToQuery(String.Empty));

            if (redirectUri.Authority.Equals(baseUrl.Authority) &&
                redirectUri.Scheme.Equals(baseUrl.Scheme))
            {
                strRedirectSafeUrl = redirectUri.AbsoluteUri;
            }
        }

        if (strRedirectSafeUrl == null){
            strRedirectSafeUrl = "default.aspx" + PageContentsHelper.AppendTenantIdToQuery(String.Empty);
        }

        Response.Redirect(strRedirectSafeUrl, false);
    }

    void Page_PreInit(object Sender, EventArgs e){
        // Deny requests with "additional path information"
        if (Request.PathInfo.Length != 0)
        {
           Response.StatusCode = 404;
           Response.End();
        }

        // gives us https://<hostname>[:port]/rdweb/pages/<lang>/
        baseUrl = new Uri(new Uri(PageContentsHelper.GetBaseUri(Context), Request.FilePath), ".");
        sLocalHelp = ConfigurationManager.AppSettings["LocalHelp"];
        if ((sLocalHelp != null) && (sLocalHelp == "true")){
            sHelpSourceServer = "./rap-help.htm";
        }
        else{
            sHelpSourceServer = "http://go.microsoft.com/fwlink/?LinkId=141038";
        }
    }
    
    void login_required(){
        username = (string)Session["DomainUserName"];
        password = (string)Session["UserPass"];

        if(username == null || username == "" || password == null || password == ""){
            logoff();
        }
    }

    void Page_Load(object sender, EventArgs e){
        login_required();

        btnSignIn.Text = L_SubmitLabel_Text;
        btnCancel.Text = L_CancelLabel_Text;

        if (Page.IsPostBack){
            return;
        }

        String deliveryMethod = (string)Session["Delivery"];

        RADIUSAttributes atts = new RADIUSAttributes();
        if (deliveryMethod != null){
            deliveryLabel.Text = deliveryMethod;
            VendorSpecificAttribute vsa = new VendorSpecificAttribute(VendorSpecificType.Generic, deliveryMethod);
            vsa.SetRADIUSAttribute(ref atts);
        }
        
        RADIUSClient client = new RADIUSClient(radiusServer, 1812, radiusSecret);
        RADIUSPacket response = client.Authenticate(username, password, atts);

        if (response == null) {
            Session["Message"] = "No response from RADIUS server";
            logoff();
        }
        
        onRadiusResponse(response);
    }    
}
