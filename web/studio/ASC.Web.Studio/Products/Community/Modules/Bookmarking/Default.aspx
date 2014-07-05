﻿<%@ Assembly Name="ASC.Web.Community.Bookmarking" %>

<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="ASC.Web.Community.Bookmarking.Bookmarking" MasterPageFile="~/Products/Community/Community.master" %>

<asp:Content ID="BookmarkingPageContent" ContentPlaceHolderID="CommunityPageContent" runat="server">
    <asp:PlaceHolder ID="BookmarkingPageContent" runat="server"></asp:PlaceHolder>
</asp:Content>

<asp:Content ID="BookmarkingSidePanel" ContentPlaceHolderID="CommunitySidePanel" runat="server">
    <asp:PlaceHolder ID="BookmarkingSideHolder" runat="server"></asp:PlaceHolder>
</asp:Content>