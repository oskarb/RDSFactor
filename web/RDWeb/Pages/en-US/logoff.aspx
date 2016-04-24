<% @Page Language="C#" Debug="true" %>

<script language="C#" runat=server>

    void Page_Load(object sender, EventArgs e)
    {
        // Deny requests with "additional path information"
        if (Request.PathInfo.Length != 0)
        {
            Response.StatusCode = 404;
            Response.End();
        }

        string strQueryString = "";

        if ( HttpContext.Current.User.Identity.IsAuthenticated == true )
        {
            Response.Redirect( "default.aspx" );
        }
        else
        {
            if ( Request.QueryString != null )
            {
                NameValueCollection objQueryString = Request.QueryString;

                if ( objQueryString["Error"] != null )
                {
                    if ( objQueryString["Error"].Equals("WkSInUse", StringComparison.CurrentCultureIgnoreCase) )
                    {
                        strQueryString = "?Error=WkSInUse";
                    }
                    else if ( objQueryString["Error"].Equals("WkSDisconnected", StringComparison.CurrentCultureIgnoreCase) )
                    {
                        strQueryString = "?Error=WkSDisconnected";
                    }
                }
            }
            Response.Redirect( "login.aspx" + strQueryString );
        }
    }

</script>
