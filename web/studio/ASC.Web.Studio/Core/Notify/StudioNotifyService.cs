/*
 *
 * (c) Copyright Ascensio System Limited 2010-2016
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/


using ASC.Common.Data;
using ASC.Common.Data.Sql;
using ASC.Common.Data.Sql.Expressions;
using ASC.Common.Security.Authentication;
using ASC.Common.Utils;
using ASC.Core;
using ASC.Core.Billing;
using ASC.Core.Tenants;
using ASC.Core.Users;
using ASC.Feed;
using ASC.Feed.Data;
using ASC.Notify;
using ASC.Notify.Model;
using ASC.Notify.Patterns;
using ASC.Notify.Recipients;
using ASC.Security.Cryptography;
using ASC.Web.Core;
using ASC.Web.Core.Users;
using ASC.Web.Core.WhiteLabel;
using ASC.Web.Studio.Utility;
using log4net;
using Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Web;
using System.Web.Configuration;

namespace ASC.Web.Studio.Core.Notify
{
    public class StudioNotifyService
    {
        private readonly INotifyClient client;
        internal readonly StudioNotifySource source;

        private static string EMailSenderName { get { return ASC.Core.Configuration.Constants.NotifyEMailSenderSysName; } }

        public static StudioNotifyService Instance
        {
            get;
            private set;
        }


        static StudioNotifyService()
        {
            Instance = new StudioNotifyService();
        }


        private StudioNotifyService()
        {
            source = new StudioNotifySource();
            client = WorkContext.NotifyContext.NotifyService.RegisterClient(source);
        }

        public void RegisterSendMethod()
        {
            if (WebConfigurationManager.AppSettings["core.notify.tariff"] != "false")
            {
                client.RegisterSendMethod(SendTariffLetters, "0 0 5 ? * *"); // 5am every day
            }
            if (CoreContext.Configuration.Personal)
            {
                client.RegisterSendMethod(SendLettersPersonal, "0 0 5 ? * *");
            }
            else
            {
                client.RegisterSendMethod(SendMsgWhatsNew, "0 0 * ? * *"); // every hour
            }
        }



        public void SendMsgWhatsNew(DateTime scheduleDate)
        {
            StudioWhatsNewService.Instance.SendMsgWhatsNew(scheduleDate, client);
        }

        public bool IsSubscribeToAdminNotify(Guid userID)
        {
            return source.GetSubscriptionProvider().IsSubscribed(Constants.ActionAdminNotify, ToRecipient(userID), null);
        }

        public void SubscribeToAdminNotify(Guid userID, bool subscribe)
        {
            var recipient = source.GetRecipientsProvider().GetRecipient(userID.ToString());
            if (recipient != null)
            {
                if (subscribe)
                {
                    source.GetSubscriptionProvider().Subscribe(Constants.ActionAdminNotify, null, recipient);
                }
                else
                {
                    source.GetSubscriptionProvider().UnSubscribe(Constants.ActionAdminNotify, null, recipient);
                }
            }
        }


        public void SendMsgToAdminAboutProfileUpdated()
        {
            client.SendNoticeAsync(Constants.ActionSelfProfileUpdated, null, null);
        }

        public void SendMsgToAdminFromNotAuthUser(string email, string message)
        {
            client.SendNoticeAsync(Constants.ActionUserMessageToAdmin, null, null,
                new TagValue(Constants.TagBody, message), new TagValue(Constants.TagUserEmail, email));
        }

        public void SendToAdminSmsCount(int balance)
        {
            client.SendNoticeAsync(Constants.ActionSmsBalance, null, null,
                new TagValue(Constants.TagBody, balance));
        }

        public void SendRequestTariff(bool license, string fname, string lname, string title, string email, string phone, string ctitle, string csize, string site, string message)
        {
            fname = (fname ?? "").Trim();
            if (string.IsNullOrEmpty(fname)) throw new ArgumentNullException("fname");
            lname = (lname ?? "").Trim();
            if (string.IsNullOrEmpty(lname)) throw new ArgumentNullException("lname");
            title = (title ?? "").Trim();
            email = (email ?? "").Trim();
            if (string.IsNullOrEmpty(email)) throw new ArgumentNullException("email");
            phone = (phone ?? "").Trim();
            if (string.IsNullOrEmpty(phone)) throw new ArgumentNullException("phone");
            ctitle = (ctitle ?? "").Trim();
            if (string.IsNullOrEmpty(ctitle)) throw new ArgumentNullException("ctitle");
            csize = (csize ?? "").Trim();
            if (string.IsNullOrEmpty(csize)) throw new ArgumentNullException("csize");
            site = (site ?? "").Trim();
            if (string.IsNullOrEmpty(site)) throw new ArgumentNullException("site");
            message = (message ?? "").Trim();

            var salesEmail = AdditionalWhiteLabelSettings.Instance.SalesEmail ?? SetupInfo.SalesEmail;

            var recipient = (IRecipient)(new DirectRecipient(SecurityContext.CurrentAccount.ID.ToString(), String.Empty, new[] { salesEmail }, false));

            client.SendNoticeToAsync(license ? Constants.ActionRequestLicense : Constants.ActionRequestTariff,
                                     null,
                                     new[] { recipient },
                                     new[] { "email.sender" },
                                     null,
                                     new TagValue(Constants.TagUserName, fname),
                                     new TagValue(Constants.TagUserLastName, lname),
                                     new TagValue(Constants.TagUserPosition, title),
                                     new TagValue(Constants.TagUserEmail, email),
                                     new TagValue(Constants.TagPhone, phone),
                                     new TagValue(Constants.TagWebsite, site),
                                     new TagValue(Constants.TagCompanyTitle, ctitle),
                                     new TagValue(Constants.TagCompanySize, csize),
                                     new TagValue(Constants.TagBody, message));
        }

        #region Voip

        public void SendToAdminVoipWarning(double balance)
        {
            client.SendNoticeAsync(Constants.ActionVoipWarning, null, null,
                new TagValue(Constants.TagBody, balance));
        }

        public void SendToAdminVoipBlocked()
        {
            client.SendNoticeAsync(Constants.ActionVoipBlocked, null, null);
        }

        #endregion

        #region User Password

        public void UserPasswordChange(UserInfo userInfo)
        {
            var hash = Hasher.Base64Hash(CoreContext.Authentication.GetUserPasswordHash(userInfo.ID));
            client.SendNoticeToAsync(
                CoreContext.Configuration.Personal ? Constants.ActionPasswordChangePersonal : Constants.ActionPasswordChange,
                        null,
                        RecipientFromEmail(new[] { userInfo.Email }, false),
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagUserName, SecurityContext.IsAuthenticated ? DisplayUserSettings.GetFullUserName(SecurityContext.CurrentAccount.ID) : ((HttpContext.Current != null) ? HttpContext.Current.Request.UserHostAddress : null)),
                        new TagValue(Constants.TagInviteLink, CommonLinkUtility.GetConfirmationUrl(userInfo.Email, ConfirmType.PasswordChange, hash)),
                        new TagValue(Constants.TagBody, string.Empty),
                        new TagValue(Constants.TagUserDisplayName, userInfo.DisplayUserName()),
                        Constants.TagSignatureStart,
                        Constants.TagSignatureEnd,
                        new TagValue(CommonTags.WithPhoto, CoreContext.Configuration.Personal ? "personal" : ""),
                        new TagValue(CommonTags.IsPromoLetter, CoreContext.Configuration.Personal ? "true" : "false"),
                        Constants.UnsubscribeLink);
        }

        public void UserPasswordChanged(Guid userID, string password)
        {
            var author = CoreContext.UserManager.GetUsers(SecurityContext.CurrentAccount.ID);
            var user = CoreContext.UserManager.GetUsers(userID);

            ISendInterceptor initInterceptor = null;
            if (!ASC.Core.Users.Constants.LostUser.Equals(author))
            {
                initInterceptor = new InitiatorInterceptor(new[] { ToRecipient(author.ID) });
                client.AddInterceptor(initInterceptor);
            }

            client.SendNoticeToAsync(
                           Constants.ActionPasswordChanged,
                           null,
                           new[] { ToRecipient(user.ID) },
                           new[] { EMailSenderName },
                           null,
                           new TagValue(Constants.TagUserName, user.DisplayUserName()),
                           new TagValue(Constants.TagUserEmail, user.Email),
                           new TagValue(Constants.TagMyStaffLink, GetMyStaffLink()),
                           new TagValue(Constants.TagPassword, password));

            if (initInterceptor != null)
            {
                client.RemoveInterceptor(initInterceptor.Name);
            }
        }

        public void SendUserPassword(UserInfo ui, string password)
        {
            client.SendNoticeToAsync(
                        Constants.ActionSendPassword,
                        null,
                        new[] { ToRecipient(ui.ID) },
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagPassword, password),
                        new TagValue(Constants.TagUserName, ui.DisplayUserName()),
                        new TagValue(Constants.TagUserEmail, ui.Email),
                        new TagValue(Constants.TagMyStaffLink, GetMyStaffLink()),
                        new TagValue(Constants.TagAuthor, (HttpContext.Current != null) ? HttpContext.Current.Request.UserHostAddress : null));
        }

        #endregion

        #region User Email

        public void SendEmailChangeInstructions(UserInfo user, string email)
        {
            client.SendNoticeToAsync(
                CoreContext.Configuration.Personal ? Constants.ActionEmailChangePersonal : Constants.ActionEmailChange,
                        null,
                        RecipientFromEmail(new[] { email }, false),
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagUserName, SecurityContext.IsAuthenticated ? DisplayUserSettings.GetFullUserName(SecurityContext.CurrentAccount.ID) : ((HttpContext.Current != null) ? HttpContext.Current.Request.UserHostAddress : null)),
                        new TagValue(Constants.TagInviteLink, CommonLinkUtility.GetConfirmationUrl(email, ConfirmType.EmailChange)),
                        new TagValue(Constants.TagBody, string.Empty),
                        new TagValue(Constants.TagUserDisplayName, string.Empty),
                        Constants.TagSignatureStart,
                        Constants.TagSignatureEnd,
                        new TagValue(CommonTags.WithPhoto, CoreContext.Configuration.Personal ? "personal" : ""),
                        new TagValue(CommonTags.IsPromoLetter, CoreContext.Configuration.Personal ? "true" : "false"),
                        new TagValue(CommonTags.Culture, user.GetCulture().Name),
                        Constants.UnsubscribeLink);
        }

        public void SendEmailActivationInstructions(UserInfo user, string email)
        {
            client.SendNoticeToAsync(
                        Constants.ActionActivateEmail,
                        null,
                        RecipientFromEmail(new[] { email }, false),
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagUserName, SecurityContext.IsAuthenticated ? DisplayUserSettings.GetFullUserName(SecurityContext.CurrentAccount.ID) : ((HttpContext.Current != null) ? HttpContext.Current.Request.UserHostAddress : null)),
                        new TagValue(Constants.TagInviteLink, CommonLinkUtility.GetConfirmationUrl(email, ConfirmType.EmailActivation)),
                        new TagValue(Constants.TagBody, string.Empty),
                        new TagValue(Constants.TagUserDisplayName, (user.DisplayUserName() ?? string.Empty).Trim()),
                        new TagValue(CommonTags.WithPhoto, CoreContext.Configuration.Personal ? "personal" : "common"),
                        new TagValue(CommonTags.IsPromoLetter, CoreContext.Configuration.Personal ? "true" : "false"),
                        Constants.UnsubscribeLink);
        }

        #endregion

        public void SendMsgMobilePhoneChange(UserInfo userInfo)
        {
            client.SendNoticeToAsync(
                Constants.ActionPhoneChange,
                null,
                RecipientFromEmail(new[] { userInfo.Email.ToLower() }, false),
                new[] { EMailSenderName },
                null,
                new TagValue(Constants.TagInviteLink, CommonLinkUtility.GetConfirmationUrl(userInfo.Email.ToLower(), ConfirmType.PhoneActivation)),
                new TagValue(Constants.TagUserDisplayName, userInfo.DisplayUserName()));
        }


        public void UserHasJoin()
        {
            client.SendNoticeAsync(Constants.ActionUserHasJoin, null, null);
        }

        public void InviteUsers(string emailList, string inviteMessage, bool join, EmployeeType emplType)
        {
            if (string.IsNullOrWhiteSpace(emailList))
            {
                return;
            }

            foreach (var email in emailList.Split(new[] { " ", ",", ";", Environment.NewLine, "\n", "\n\r" }, StringSplitOptions.RemoveEmptyEntries))
            {
                SendInvite(new UserInfo() { Email = email }, inviteMessage, join, emplType);
            }
        }

        private void SendInvite(UserInfo user, string inviteMessage, bool join, EmployeeType emplType)
        {
            var inviteUrl = CommonLinkUtility.GetConfirmationUrl(user.Email, ConfirmType.EmpInvite, (int)emplType, SecurityContext.CurrentAccount.ID)
                            + String.Format("&firstname={0}&lastname={1}&emplType={2}",
                                            HttpUtility.UrlEncode(user.FirstName),
                                            HttpUtility.UrlEncode(user.LastName),
                                            (int)emplType);

            client.SendNoticeToAsync(
                        join ? Constants.ActionJoinUsers : Constants.ActionInviteUsers,
                        null,
                        RecipientFromEmail(new string[] { user.Email }, join),/*if it's invite - don't check activation status*/
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagUserName, SecurityContext.IsAuthenticated ? DisplayUserSettings.GetFullUserName(SecurityContext.CurrentAccount.ID) : ((HttpContext.Current != null) ? HttpContext.Current.Request.UserHostAddress : null)),
                        new TagValue(Constants.TagInviteLink, inviteUrl),
                        new TagValue(Constants.TagBody, inviteMessage ?? string.Empty),
                        new TagValue(CommonTags.WithPhoto, "common"),
                        new TagValue(Constants.TagUserDisplayName, (user.DisplayUserName() ?? "").Trim()),
                        CreateSendFromTag());
        }

        public void UserInfoAddedAfterInvite(UserInfo newUserInfo, string password)
        {
            if (CoreContext.UserManager.UserExists(newUserInfo.ID))
            {
                var tenant = CoreContext.TenantManager.GetCurrentTenant();
                var tariff = CoreContext.TenantManager.GetTenantQuota(tenant.TenantId);
                var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

                var notifyAction = Constants.ActionYouAddedAfterInvite;
                var footer = "common";

                if (CoreContext.Configuration.Personal)
                    notifyAction = Constants.ActionAfterRegistrationPersonal1;
                else if (TenantExtra.Enterprise)
                    notifyAction = defaultRebranding ? Constants.ActionYouAddedAfterInviteEnterprise : Constants.ActionYouAddedAfterInviteWhitelabel;
                else if (tariff.Free)
                    notifyAction = Constants.ActionYouAddedAfterInviteFreeCloud;

                if (CoreContext.Configuration.Personal)
                    footer = "personal";
                else if (TenantExtra.Enterprise)
                    footer = "common";
                else if (tariff.Free)
                    footer = "freecloud";


                var greenButtonText = TenantExtra.Enterprise ? WebstudioNotifyPatternResource.ButtonAccessYourPortal : WebstudioNotifyPatternResource.ButtonAccessYouWebOffice;

                client.SendNoticeToAsync(
                    notifyAction,
                    null,
                    RecipientFromEmail(new[] { newUserInfo.Email }, false),
                    new[] { EMailSenderName },
                    null,
                    new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                    new TagValue(Constants.TagUserEmail, newUserInfo.Email),
                    new TagValue(Constants.TagMyStaffLink, GetMyStaffLink()),
                    new TagValue(Constants.TagPassword, password),
                    Constants.TagMarkerStart,
                    Constants.TagMarkerEnd,
                    Constants.TagFrameStart,
                    Constants.TagFrameEnd,
                    Constants.TagHeaderStart,
                    Constants.TagHeaderEnd,
                    Constants.TagStrongStart,
                    Constants.TagStrongEnd,
                    Constants.TagSignatureStart,
                    Constants.TagSignatureEnd,
                    Constants.TagGreenButton(greenButtonText, CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/')),
                    new TagValue(CommonTags.WithPhoto, footer),
                    new TagValue(CommonTags.IsPromoLetter, CoreContext.Configuration.Personal ? "true" : "false"),
                    Constants.UnsubscribeLink);
            }
        }

        public void GuestInfoAddedAfterInvite(UserInfo newUserInfo, string password)
        {
            if (CoreContext.UserManager.UserExists(newUserInfo.ID))
            {
                var tariff = CoreContext.TenantManager.GetTenantQuota(CoreContext.TenantManager.GetCurrentTenant().TenantId);
                var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

                var notifyAction =
                    TenantExtra.Enterprise
                        ? defaultRebranding
                              ? Constants.ActionYouAddedLikeGuestEnterprise
                              : Constants.ActionYouAddedLikeGuestWhitelabel
                        : (tariff != null && tariff.Free)
                              ? Constants.ActionYouAddedLikeGuestFreeCloud
                              : Constants.ActionYouAddedLikeGuest;


                var greenButtonText = TenantExtra.Enterprise ? WebstudioNotifyPatternResource.ButtonAccessYourPortal : WebstudioNotifyPatternResource.ButtonAccessYouWebOffice;

                client.SendNoticeToAsync(
                            notifyAction,
                            null,
                            RecipientFromEmail(new[] { newUserInfo.Email }, false),
                            new[] { EMailSenderName },
                            null,
                            new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                            new TagValue(Constants.TagUserEmail, newUserInfo.Email),
                            new TagValue(Constants.TagMyStaffLink, GetMyStaffLink()),
                            Constants.TagGreenButton(greenButtonText, CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/')),
                            new TagValue(Constants.TagPassword, password));
            }
        }

        public void UserInfoActivation(UserInfo newUserInfo)
        {
            if (newUserInfo.IsActive)
            {
                throw new ArgumentException("User is already activated!");
            }

            var tariff = CoreContext.TenantManager.GetTenantQuota(CoreContext.TenantManager.GetCurrentTenant().TenantId);
            var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

            var notifyAction =
                TenantExtra.Enterprise
                    ? defaultRebranding
                          ? Constants.ActionActivateUsersEnterprise
                          : Constants.ActionActivateUsersWhitelabel
                    : (tariff != null && tariff.Free)
                          ? Constants.ActionActivateUsersFreeCloud
                          : Constants.ActionActivateUsers;


            var footer = !TenantExtra.Enterprise && tariff.Free ? "freecloud" : "common";

            client.SendNoticeToAsync(
                notifyAction,
                null,
                RecipientFromEmail(new[] { newUserInfo.Email.ToLower() }, false),
                new[] { EMailSenderName },
                null,
                new TagValue(Constants.TagInviteLink, GenerateActivationConfirmUrl(newUserInfo)),
                new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                new TagValue(CommonTags.WithPhoto, footer),
                new TagValue(CommonTags.IsPromoLetter, "false"),
                new TagValue("noUnsubscribeLink", "true"),
                CreateSendFromTag());
        }

        public void GuestInfoActivation(UserInfo newUserInfo)
        {
            if (newUserInfo.IsActive)
            {
                throw new ArgumentException("User is already activated!");
            }

            var tariff = CoreContext.TenantManager.GetTenantQuota(CoreContext.TenantManager.GetCurrentTenant().TenantId);
            var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

            var notifyAction =
                TenantExtra.Enterprise
                    ? defaultRebranding
                          ? Constants.ActionActivateGuestsEnterprise
                          : Constants.ActionActivateGuestsWhitelabel
                    : (tariff != null && tariff.Free)
                          ? Constants.ActionActivateGuestsFreeCloud
                          : Constants.ActionActivateGuests;

            var footer = !TenantExtra.Enterprise && tariff.Free ? "freecloud" : "common";

            client.SendNoticeToAsync(
                notifyAction,
                null,
                RecipientFromEmail(new[] { newUserInfo.Email.ToLower() }, false),
                new[] { EMailSenderName },
                null,
                new TagValue(Constants.TagInviteLink, GenerateActivationConfirmUrl(newUserInfo)),
                new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                new TagValue(CommonTags.WithPhoto, footer),
                new TagValue("noUnsubscribeLink", "true"),
                CreateSendFromTag());
        }

        public void SendMsgProfileDeletion(string email)
        {
            client.SendNoticeToAsync(
                        Constants.ActionProfileDelete,
                        null,
                        RecipientFromEmail(new[] { email }, false),
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagInviteLink, CommonLinkUtility.GetConfirmationUrl(email, ConfirmType.ProfileRemove)));
        }

        public void SendAdminWellcome(UserInfo newUserInfo)
        {
            if (CoreContext.UserManager.UserExists(newUserInfo.ID))
            {
                if (!newUserInfo.IsActive)
                {
                    throw new ArgumentException("User is not activated yet!");
                }

                var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

                var notifyAction =
                    TenantExtra.Enterprise
                        ? defaultRebranding
                              ? Constants.ActionAdminWellcomeEnterprise
                              : Constants.ActionAdminWellcomeWhitelabel
                        : Constants.ActionAdminWellcome;

                var greenBtnText =
                    TenantExtra.Enterprise
                    ? WebstudioNotifyPatternResource.ButtonAccessControlPanel
                    : WebstudioNotifyPatternResource.ButtonConfigureRightNow;

                var greenBtnLink =
                    TenantExtra.Enterprise
                    ? CommonLinkUtility.GetFullAbsolutePath("~" + SetupInfo.ControlPanelUrl)
                    : CommonLinkUtility.GetAdministration(ManagementType.General);

                var tableItemText1 = string.Empty;
                var tableItemText2 = string.Empty;
                var tableItemText3 = string.Empty;
                var tableItemText4 = string.Empty;
                var tableItemText5 = string.Empty;

                var tableItemImg1 = string.Empty;
                var tableItemImg2 = string.Empty;
                var tableItemImg3 = string.Empty;
                var tableItemImg4 = string.Empty;
                var tableItemImg5 = string.Empty;

                var tableItemComment1 = string.Empty;
                var tableItemComment2 = string.Empty;
                var tableItemComment3 = string.Empty;
                var tableItemComment4 = string.Empty;
                var tableItemComment5 = string.Empty;


                if (!TenantExtra.Enterprise)
                {
                    tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/tips-brand-100.png";
                    tableItemText1 = WebstudioNotifyPatternResource.ItemBrandYourWebOffice;
                    tableItemComment1 = WebstudioNotifyPatternResource.ItemBrandYourWebOfficeText;

                    tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/tips-regional-setings-100.png";
                    tableItemText2 = WebstudioNotifyPatternResource.ItemAdjustRegionalSettings;
                    tableItemComment2 = WebstudioNotifyPatternResource.ItemAdjustRegionalSettingsText;


                    tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/tips-customize-100.png";
                    tableItemText3 = WebstudioNotifyPatternResource.ItemCustomizeWebOfficeInterface;
                    tableItemComment3 = WebstudioNotifyPatternResource.ItemCustomizeWebOfficeInterfaceText;

                    tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/tips-modules-100.png";
                    tableItemText4 = WebstudioNotifyPatternResource.ItemModulesAndTools;
                    tableItemComment4 = WebstudioNotifyPatternResource.ItemModulesAndToolsText;

                    tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/tips-sms-secure-100.png";
                    tableItemText5 = WebstudioNotifyPatternResource.ItemSecureAccess;
                    tableItemComment5 = WebstudioNotifyPatternResource.ItemSecureAccessText;
                }

                client.SendNoticeToAsync(
                    notifyAction,
                    null,
                    RecipientFromEmail(new[] { newUserInfo.Email.ToLower() }, false),
                    new[] { EMailSenderName },
                    null,
                    new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                    Constants.TagStrongStart,
                    Constants.TagStrongEnd,
                    Constants.TagGreenButton(greenBtnText, greenBtnLink),
                    Constants.TagTableTop(),
                    Constants.TagTableItem(1, tableItemText1, string.Empty, tableItemImg1, tableItemComment1, string.Empty, string.Empty),
                    Constants.TagTableItem(2, tableItemText2, string.Empty, tableItemImg2, tableItemComment2, string.Empty, string.Empty),
                    Constants.TagTableItem(3, tableItemText3, string.Empty, tableItemImg3, tableItemComment3, string.Empty, string.Empty),
                    Constants.TagTableItem(4, tableItemText4, string.Empty, tableItemImg4, tableItemComment4, string.Empty, string.Empty),
                    Constants.TagTableItem(5, tableItemText5, string.Empty, tableItemImg5, tableItemComment5, string.Empty, string.Empty),
                    Constants.TagTableBottom(),
                    new TagValue(CommonTags.WithPhoto, "common"),
                    new TagValue(CommonTags.IsPromoLetter, "false"),
                    CreateSendFromTag());
            }
        }

        #region Backup & Restore

        public void SendMsgBackupCompleted(Guid userId, string link)
        {
            client.SendNoticeToAsync(
                Constants.ActionBackupCreated,
                null,
                new[] {ToRecipient(userId)},
                new[] {EMailSenderName},
                null,
                new TagValue(Constants.TagOwnerName, CoreContext.UserManager.GetUsers(userId).DisplayUserName()));
        }

        public void SendMsgRestoreStarted(bool notifyAllUsers)
        {
            IRecipient[] users =
                notifyAllUsers
                    ? CoreContext.UserManager.GetUsers(EmployeeStatus.Active).Select(u => ToRecipient(u.ID)).ToArray()
                    : new[] { ToRecipient(CoreContext.TenantManager.GetCurrentTenant().OwnerId) };

            client.SendNoticeToAsync(
                Constants.ActionRestoreStarted,
                null,
                users,
                new[] { EMailSenderName },
                null);
        }

        public void SendMsgRestoreCompleted(bool notifyAllUsers)
        {
            var owner = CoreContext.UserManager.GetUsers(CoreContext.TenantManager.GetCurrentTenant().OwnerId);

            IRecipient[] users =
                notifyAllUsers
                    ? CoreContext.UserManager.GetUsers(EmployeeStatus.Active).Select(u => ToRecipient(u.ID)).ToArray()
                    : new[] { ToRecipient(owner.ID) };

            client.SendNoticeToAsync(
                Constants.ActionRestoreCompleted,
                null,
                users,
                new[] {EMailSenderName},
                null,
                new TagValue(Constants.TagOwnerName, owner.DisplayUserName()));
        }

        #endregion

        #region Portal Deactivation & Deletion

        public void SendMsgPortalDeactivation(Tenant t, string d_url, string a_url)
        {
            var u = CoreContext.UserManager.GetUsers(t.OwnerId);
            client.SendNoticeToAsync(
                        Constants.ActionPortalDeactivate,
                        null,
                        new[] { u },
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagActivateUrl, a_url),
                        new TagValue(Constants.TagDeactivateUrl, d_url),
                        new TagValue(Constants.TagOwnerName, u.DisplayUserName()));
        }

        public void SendMsgPortalDeletion(Tenant t, string url, bool showAutoRenewText)
        {
            var u = CoreContext.UserManager.GetUsers(t.OwnerId);
            client.SendNoticeToAsync(
                        Constants.ActionPortalDelete,
                        null,
                        new[] { u },
                        new[] { EMailSenderName },
                        null,
                        new TagValue(Constants.TagDeleteUrl, url),
                        new TagValue(Constants.TagAutoRenew, showAutoRenewText.ToString()),
                        new TagValue(Constants.TagOwnerName, u.DisplayUserName()));
        }

        public void SendMsgPortalDeletionSuccess(Tenant t, TenantQuota tariff, string url)
        {
            var u = CoreContext.UserManager.GetUsers(t.OwnerId);

            var notifyAction = tariff != null && tariff.Free ? Constants.ActionPortalDeleteSuccessFreeCloud : Constants.ActionPortalDeleteSuccess;

            client.SendNoticeToAsync(
                        notifyAction,
                        null,
                        new[] { u },
                        new[] { EMailSenderName },
                        null,
                        new TagValue("FeedBackUrl", url),
                        new TagValue(Constants.TagOwnerName, u.DisplayUserName()));
        }

        #endregion

        public void SendMsgDnsChange(Tenant t, string confirmDnsUpdateUrl, string portalAddress, string portalDns)
        {
            var u = CoreContext.UserManager.GetUsers(t.OwnerId);
            client.SendNoticeToAsync(
                        Constants.ActionDnsChange,
                        null,
                        new[] { u },
                        new[] { EMailSenderName },
                        null,
                        new TagValue("ConfirmDnsUpdate", confirmDnsUpdateUrl),
                        new TagValue("PortalAddress", AddHttpToUrl(portalAddress)),
                        new TagValue("PortalDns", AddHttpToUrl(portalDns ?? string.Empty)),
                        new TagValue(Constants.TagOwnerName, u.DisplayUserName()));
        }


        public void SendMsgConfirmChangeOwner(Tenant t, string newOwnerName, string confirmOwnerUpdateUrl)
        {
            var u = CoreContext.UserManager.GetUsers(t.OwnerId);
            client.SendNoticeToAsync(
                        Constants.ActionConfirmOwnerChange,
                        null,
                        new[] { u },
                        new[] { EMailSenderName },
                        null,
                        new TagValue("ConfirmPortalOwnerUpdate", confirmOwnerUpdateUrl),
                        new TagValue(Constants.TagUserName, newOwnerName),
                        new TagValue(Constants.TagOwnerName, u.DisplayUserName()));
        }


        public void SendCongratulations(UserInfo u)
        {
            try
            {
            var tenant = CoreContext.TenantManager.GetCurrentTenant();
            var tariff = CoreContext.TenantManager.GetTenantQuota(tenant.TenantId);
            var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

            var notifyAction =
                TenantExtra.Enterprise
                    ? defaultRebranding
                          ? Constants.ActionCongratulationsEnterprise
                          : Constants.ActionCongratulationsWhitelabel
                    : (tariff != null && tariff.Free)
                          ? Constants.ActionCongratulationsFreeCloud
                          : Constants.ActionCongratulations;

            var footer = !TenantExtra.Enterprise && tariff != null && tariff.Free ? "freecloud" : "common";

            var confirmationUrl = CommonLinkUtility.GetConfirmationUrl(u.Email, ConfirmType.EmailActivation);


            client.SendNoticeToAsync(
                notifyAction,
                null,
                RecipientFromEmail(new[] { u.Email.ToLower() }, false),
                new[] { EMailSenderName },
                null,
                new TagValue(Constants.TagUserName, u.DisplayUserName()),
                new TagValue(Constants.TagUserEmail, u.Email),
                new TagValue(Constants.TagMyStaffLink, GetMyStaffLink()),
                new TagValue(Constants.TagSettingsLink, CommonLinkUtility.GetAdministration(ManagementType.General)),
                new TagValue(Constants.TagInviteLink, confirmationUrl),
                Constants.TagGreenButton(WebstudioNotifyPatternResource.ButtonClickForConfirm, confirmationUrl),
                new TagValue(CommonTags.WithPhoto, footer));
            }
            catch (Exception error)
            {
                LogManager.GetLogger("ASC.Notify").Error(error);
            }
        }

        public void SendTariffLetters(DateTime scheduleDate)
        {
            var log = LogManager.GetLogger("ASC.Notify");
            var now = scheduleDate.Date;
            var dbid = "webstudio";

            log.Info("Start SendTariffWarnings.");

            var defaultRebranding = MailWhiteLabelSettings.Instance.IsDefault;

            var activeTenants = CoreContext.TenantManager.GetTenants().Where(t => t.Status == TenantStatus.Active).ToList();

            if (activeTenants.Count > 0)
            {
                var monthQuotas = CoreContext.TenantManager.GetTenantQuotas()
                                .Where(r => !r.Trial && r.Visible && !r.Year && !r.Year3 && !r.Free && !r.NonProfit)
                                .ToList();
                var monthQuotasIds = monthQuotas.Select(q => q.Id).ToArray();

            foreach (var tenant in activeTenants)
            {
                try
                {
                    CoreContext.TenantManager.SetCurrentTenant(tenant.TenantId);
                    var tariff = CoreContext.PaymentManager.GetTariff(tenant.TenantId);
                    var quota = CoreContext.TenantManager.GetTenantQuota(tenant.TenantId);
                    var duedate = tariff.DueDate.Date;
                    var delayDuedate = tariff.DelayDueDate.Date;

                    INotifyAction action = null;

                    var toadmins = false;
                    var touser = false;
                    var toguest = false;

                    var footer = "common";

                    var greenButtonText = string.Empty;
                    var greenButtonUrl = string.Empty;

                    var tableItemText1 = string.Empty;
                    var tableItemText2 = string.Empty;
                    var tableItemText3 = string.Empty;
                    var tableItemText4 = string.Empty;
                    var tableItemText5 = string.Empty;

                    var tableItemUrl1 = string.Empty;
                    var tableItemUrl2 = string.Empty;
                    var tableItemUrl3 = string.Empty;
                    var tableItemUrl4 = string.Empty;
                    var tableItemUrl5 = string.Empty;

                    var tableItemImg1 = string.Empty;
                    var tableItemImg2 = string.Empty;
                    var tableItemImg3 = string.Empty;
                    var tableItemImg4 = string.Empty;
                    var tableItemImg5 = string.Empty;

                    var tableItemComment1 = string.Empty;
                    var tableItemComment2 = string.Empty;
                    var tableItemComment3 = string.Empty;
                    var tableItemComment4 = string.Empty;
                    var tableItemComment5 = string.Empty;

                    var tableItemLearnMoreText1 = string.Empty;
                    var tableItemLearnMoreText2 = string.Empty;
                    var tableItemLearnMoreText3 = string.Empty;
                    var tableItemLearnMoreText4 = string.Empty;
                    var tableItemLearnMoreText5 = string.Empty;

                    var tableItemLearnMoreUrl1 = string.Empty;
                    var tableItemLearnMoreUrl2 = string.Empty;
                    var tableItemLearnMoreUrl3 = string.Empty;
                    var tableItemLearnMoreUrl4 = string.Empty;
                    var tableItemLearnMoreUrl5 = string.Empty;

                    #region 2 days after registration to users (not admins and not guests) only

                    if ((TenantExtra.Enterprise && quota.Free || !quota.Free) && tenant.CreatedDateTime.Date.AddDays(2) == now && defaultRebranding)
                    {
                        action = !TenantExtra.Enterprise ? Constants.ActionAfterCreation4 : Constants.ActionAfterCreation4Enterprise;
                        touser = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/move-to-cloud-01-50.png";
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemAddFilesCreatWorkspace;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/move-to-cloud-02-50.png";
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemTryOnlineDocEditor;

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/move-to-cloud-03-50.png";
                        tableItemComment3 = WebstudioNotifyPatternResource.ItemUploadCrmContacts;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/move-to-cloud-04-50.png";
                        tableItemComment4 = WebstudioNotifyPatternResource.ItemAddTeamlabMail;

                        tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/move-to-cloud-05-50.png";
                        tableItemComment5 = WebstudioNotifyPatternResource.ItemIntegrateIM;

                        greenButtonText = !TenantExtra.Enterprise ? WebstudioNotifyPatternResource.ButtonMoveRightNow : WebstudioNotifyPatternResource.ButtonAccessYourPortal;
                        greenButtonUrl = CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/');
                    }

                    #endregion

                    #region 3 days after registration to admins SAAS + only 1 user

                    else if (!TenantExtra.Enterprise && tenant.CreatedDateTime.Date.AddDays(3) == now && !quota.Free && CoreContext.UserManager.GetUsers().Count() == 1)
                    {
                        action = Constants.ActionAfterCreation1;
                        toadmins = true;

                        footer = "common";

                        greenButtonText = WebstudioNotifyPatternResource.ButtonInviteRightNow;
                        greenButtonUrl = String.Format("{0}/products/people/", CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/'));
                    }

                    #endregion

                    #region 3 days after registration to admins FREECLOUD

                    else if (!TenantExtra.Enterprise && tenant.CreatedDateTime.Date.AddDays(3) == now && quota.Free)
                    {
                        action = Constants.ActionAfterCreation1FreeCloud;
                        footer = "freecloud";
                        toadmins = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/integrate_documents.jpg";
                        tableItemText1 = WebstudioNotifyPatternResource.ItemCreateWorkspaceDocs;
                        tableItemUrl1 = "http://helpcenter.onlyoffice.com/tipstricks/add-resource.aspx?utm_medium=newsletter&utm_source=after_signup_1&utm_campaign=email";

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/import_projects.jpg";
                        tableItemText2 = WebstudioNotifyPatternResource.ItemImportProjectsBasecamp;
                        tableItemUrl2 = "http://helpcenter.onlyoffice.com/tipstricks/basecamp-import.aspx?utm_medium=newsletter&utm_source=after_signup_1&utm_campaign=email";

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/csv.jpg";
                        tableItemText3 = WebstudioNotifyPatternResource.ItemUploadCrmContactsCsv;
                        tableItemUrl3 = "http://helpcenter.onlyoffice.com/guides/import-contacts.aspx?utm_medium=newsletter&utm_source=after_signup_1&utm_campaign=email";

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/mail.jpg";
                        tableItemText4 = WebstudioNotifyPatternResource.ItemAddTeamlabMail;
                        tableItemUrl4 = "http://helpcenter.onlyoffice.com/gettingstarted/mail.aspx?utm_medium=newsletter&utm_source=after_signup_1&utm_campaign=email";
                    }

                    #endregion

                    #region 3 days after registration to admins ENTERPRISE if FREE

                    else if (TenantExtra.Enterprise && quota.Free && tenant.CreatedDateTime.Date.AddDays(3) == now && defaultRebranding)
                    {
                        action = Constants.ActionAfterCreation1Enterprise;
                        footer = "common";
                        toadmins = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/tips-brand-100.png";
                        tableItemText1 = WebstudioNotifyPatternResource.ItemBrandYourWebOffice;
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemBrandYourWebOfficeText;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/tips-regional-setings-100.png";
                        tableItemText2 = WebstudioNotifyPatternResource.ItemAdjustRegionalSettings;
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemAdjustRegionalSettingsText;


                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/tips-customize-100.png";
                        tableItemText3 = WebstudioNotifyPatternResource.ItemCustomizeWebOfficeInterface;
                        tableItemComment3 = WebstudioNotifyPatternResource.ItemCustomizeWebOfficeInterfaceText;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/tips-modules-100.png";
                        tableItemText4 = WebstudioNotifyPatternResource.ItemModulesAndTools;
                        tableItemComment4 = WebstudioNotifyPatternResource.ItemModulesAndToolsText;


                        greenButtonText = WebstudioNotifyPatternResource.ButtonConfigureRightNow;
                        greenButtonUrl = CommonLinkUtility.GetAdministration(ManagementType.General);
                    }

                    #endregion

                    #region 4 days after registration to admins ENTERPRISE + only 1 user + only FREE

                    else if (TenantExtra.Enterprise && quota.Free && tenant.CreatedDateTime.Date.AddDays(4) == now && CoreContext.UserManager.GetUsers().Count() == 1 && defaultRebranding)
                    {
                        action = Constants.ActionAfterCreation8Enterprise;
                        footer = "common";
                        toadmins = true;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonInviteRightNow;
                        greenButtonUrl = String.Format("{0}/products/people/", CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/'));
                    }

                    #endregion

                    #region 7 days after registration

                    else if ((TenantExtra.Enterprise && quota.Free || !quota.Free) && tenant.CreatedDateTime.Date.AddDays(7) == now && defaultRebranding)
                    {
                        action = !TenantExtra.Enterprise ? Constants.ActionAfterCreation6 : Constants.ActionAfterCreation6Enterprise;
                        toadmins = true;
                        touser = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/tips-documents-01-100.png";
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemFeatureTips1CoEditingText;
                        tableItemLearnMoreUrl1 = "https://helpcenter.onlyoffice.com/ONLYOFFICE-Editors/ONLYOFFICE-Document-Editor/HelpfulHints/CollaborativeEditing.aspx";
                        tableItemLearnMoreText1 = WebstudioNotifyPatternResource.LinkLearnMore;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/tips-documents-02-100.png";
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemFeatureTips2VersionHistoryText;
                        tableItemLearnMoreUrl2 = "http://helpcenter.onlyoffice.com/ONLYOFFICE-Editors/ONLYOFFICE-Document-Editor/UsageInstructions/ViewDocInfo.aspx";
                        tableItemLearnMoreText2 = WebstudioNotifyPatternResource.LinkLearnMore;

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/tips-documents-03-100.png";
                        tableItemComment3 = !TenantExtra.Enterprise ? WebstudioNotifyPatternResource.ItemFeatureTips3ShareDocsText : WebstudioNotifyPatternResource.ItemFeatureTips3ShareDocsTextEnterprise;
                        tableItemLearnMoreUrl3 = "https://helpcenter.onlyoffice.com/gettingstarted/documents.aspx#SharingDocuments_block";
                        tableItemLearnMoreText3 = WebstudioNotifyPatternResource.LinkLearnMore;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/tips-documents-04-100.png";
                        tableItemComment4 = !TenantExtra.Enterprise ?
                            WebstudioNotifyPatternResource.ItemFeatureTips4CloudStoragesText :
                            WebstudioNotifyPatternResource.ItemFeatureTips4MailMergeText;
                        tableItemLearnMoreUrl4 = !TenantExtra.Enterprise ?
                            "https://helpcenter.onlyoffice.com/gettingstarted/documents.aspx#SharingDocuments_block" :
                            "https://helpcenter.onlyoffice.com/ONLYOFFICE-Editors/ONLYOFFICE-Document-Editor/UsageInstructions/UseMailMerge.aspx";
                        tableItemLearnMoreText4 = WebstudioNotifyPatternResource.LinkLearnMore;

                        tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/tips-documents-05-100.png";
                        tableItemComment5 = WebstudioNotifyPatternResource.ItemFeatureTips5iOSText;
                        tableItemLearnMoreUrl5 = "https://itunes.apple.com/us/app/onlyoffice-documents/id944896972";
                        tableItemLearnMoreText5 = WebstudioNotifyPatternResource.ButtonGoToAppStore;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonAccessYouWebOffice;
                        greenButtonUrl = String.Format("{0}/products/files/", CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/'));
                    }

                    #endregion

                    #region 2 weeks after registration

                    else if ((TenantExtra.Enterprise && quota.Free || !quota.Free) && tenant.CreatedDateTime.Date.AddDays(14) == now && defaultRebranding)
                    {
                        action = !TenantExtra.Enterprise ? Constants.ActionAfterCreation2 : Constants.ActionAfterCreation2Enterprise;
                        toadmins = true;
                        touser = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/mail-exp-01-100.png";
                        tableItemText1 = WebstudioNotifyPatternResource.ItemFeatureMailGroups;
                        tableItemUrl1 = "https://helpcenter.onlyoffice.com/tipstricks/alias-groups.aspx";
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemFeatureMailGroupsText;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/mail-exp-02-100.png";
                        tableItemText2 = WebstudioNotifyPatternResource.ItemFeatureMailboxAliases;
                        tableItemUrl2 = "https://helpcenter.onlyoffice.com/tipstricks/alias-groups.aspx";
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemFeatureMailboxAliasesText;

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/mail-exp-03-100.png";
                        tableItemText3 = WebstudioNotifyPatternResource.ItemFeatureEmailSignature;
                        tableItemUrl3 = "https://helpcenter.onlyoffice.com/gettingstarted/mail.aspx#SendingReceivingMessages_block__addingSignature";
                        tableItemComment3 = WebstudioNotifyPatternResource.ItemFeatureEmailSignatureText;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/mail-exp-04-100.png";
                        tableItemText4 = WebstudioNotifyPatternResource.ItemFeatureLinksVSAttachments;
                        tableItemUrl4 = "https://helpcenter.onlyoffice.com/gettingstarted/mail.aspx#SendingReceivingMessages_block";
                        tableItemComment4 = WebstudioNotifyPatternResource.ItemFeatureLinksVSAttachmentsText;

                        tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/mail-exp-05-100.png";
                        tableItemText5 = WebstudioNotifyPatternResource.ItemFeatureFolderForAtts;
                        tableItemUrl5 = "https://helpcenter.onlyoffice.com/gettingstarted/mail.aspx#SendingReceivingMessages_block__emailIn";
                        tableItemComment5 = WebstudioNotifyPatternResource.ItemFeatureFolderForAttsText;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonAccessMail;
                        greenButtonUrl = String.Format("{0}/addons/mail/", CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/'));
                    }

                    #endregion

                    #region 3 weeks after registration

                    else if ((TenantExtra.Enterprise && quota.Free || !quota.Free) && tenant.CreatedDateTime.Date.AddDays(21) == now && defaultRebranding)
                    {
                        action = !TenantExtra.Enterprise ? Constants.ActionAfterCreation3 : Constants.ActionAfterCreation3Enterprise;
                        toadmins = true;
                        touser = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/crm-01-100.png";
                        tableItemText1 = WebstudioNotifyPatternResource.ItemWebToLead;
                        tableItemUrl1 = "https://helpcenter.onlyoffice.com/tipstricks/website-contact-form.aspx";
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemWebToLeadText;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/crm-02-100.png";
                        tableItemText2 = WebstudioNotifyPatternResource.ItemARM;
                        tableItemUrl2 = "https://helpcenter.onlyoffice.com/gettingstarted/crm.aspx#AddingContacts_block";
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemARMText;

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/crm-03-100.png";
                        tableItemText3 = WebstudioNotifyPatternResource.ItemCustomization;
                        tableItemUrl3 = "https://helpcenter.onlyoffice.com/gettingstarted/crm.aspx#AddingContacts_block";
                        tableItemComment3 = WebstudioNotifyPatternResource.ItemCustomizationText;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/crm-04-100.png";
                        tableItemText4 = WebstudioNotifyPatternResource.ItemLinkWithProjects;
                        tableItemUrl4 = "https://helpcenter.onlyoffice.com/guides/link-with-project.aspx";
                        tableItemComment4 = WebstudioNotifyPatternResource.ItemLinkWithProjectsText;

                        tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/crm-05-100.png";
                        tableItemText5 = WebstudioNotifyPatternResource.ItemMailIntegration;
                        tableItemUrl5 = "https://helpcenter.onlyoffice.com/gettingstarted/mail.aspx#IntegratingwithCRM_block";
                        tableItemComment5 = WebstudioNotifyPatternResource.ItemMailIntegrationText;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonAccessCRMSystem;
                        greenButtonUrl = String.Format("{0}/products/crm/", CommonLinkUtility.GetFullAbsolutePath("~").TrimEnd('/'));
                    }

                    #endregion

                    #region 28 days after registration

                    else if ((TenantExtra.Enterprise && quota.Free || !TenantExtra.Enterprise) && tenant.CreatedDateTime.Date.AddDays(28) == now && defaultRebranding)
                    {
                        action = !TenantExtra.Enterprise ? Constants.ActionAfterCreation7 : Constants.ActionAfterCreation7Enterprise;
                        toadmins = true;
                        touser = true;

                        tableItemImg1 = "http://cdn.teamlab.com/media/newsletters/images/collaboration-01-100.png";
                        tableItemText1 = WebstudioNotifyPatternResource.ItemFeatureCommunity;
                        tableItemUrl1 = "http://helpcenter.onlyoffice.com/gettingstarted/community.aspx";
                        tableItemComment1 = WebstudioNotifyPatternResource.ItemFeatureCommunityText;

                        tableItemImg2 = "http://cdn.teamlab.com/media/newsletters/images/collaboration-02-100.png";
                        tableItemText2 = WebstudioNotifyPatternResource.ItemFeatureGanttChart;
                        tableItemUrl2 = "http://helpcenter.onlyoffice.com/guides/gantt-chart.aspx";
                        tableItemComment2 = WebstudioNotifyPatternResource.ItemFeatureGanttChartText;

                        tableItemImg3 = "http://cdn.teamlab.com/media/newsletters/images/collaboration-03-100.png";
                        tableItemText3 = WebstudioNotifyPatternResource.ItemFeatureProjectDiscussions;
                        tableItemUrl3 = "http://helpcenter.onlyoffice.com/gettingstarted/projects.aspx#LeadingDiscussion_block";
                        tableItemComment3 = WebstudioNotifyPatternResource.ItemFeatureProjectDiscussionsText;

                        tableItemImg4 = "http://cdn.teamlab.com/media/newsletters/images/collaboration-04-100.png";
                        tableItemText4 = WebstudioNotifyPatternResource.ItemFeatureDocCoAuthoring;
                        tableItemUrl4 = "http://helpcenter.onlyoffice.com/ONLYOFFICE-Editors/ONLYOFFICE-Document-Editor/HelpfulHints/CollaborativeEditing.aspx";
                        tableItemComment4 = WebstudioNotifyPatternResource.ItemFeatureDocCoAuthoringText;

                        tableItemImg5 = "http://cdn.teamlab.com/media/newsletters/images/collaboration-05-100.png";
                        tableItemText5 = WebstudioNotifyPatternResource.ItemFeatureTalk;
                        tableItemUrl5 = "http://helpcenter.onlyoffice.com/gettingstarted/talk.aspx";
                        tableItemComment5 = WebstudioNotifyPatternResource.ItemFeatureTalkText;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonAccessYouWebOffice;
                        greenButtonUrl = CommonLinkUtility.GetAdministration(ManagementType.ProductsAndInstruments);
                    }

                    #endregion

                    #region FREECLOUD - 30 days after registration

                    else if (quota.Free && tenant.CreatedDateTime.Date.AddDays(30) == now)
                    {
                        action = Constants.ActionAfterCreation30FreeCloud;
                        toadmins = true;

                        footer = "freecloud";
                    }

                    #endregion

                    #region Trial warning letters

                    #region 5 days before trial ends

                    else if (quota.Trial && duedate != DateTime.MaxValue && duedate.AddDays(-5) == now)
                    {
                        action = Constants.ActionTariffWarningTrial;
                        toadmins = true;

                        greenButtonText = WebstudioNotifyPatternResource.ButtonSelectPricingPlans;
                        greenButtonUrl = CommonLinkUtility.GetFullAbsolutePath("~/tariffs.aspx");
                    }

                    #endregion

                    #region trial expires today

                    else if (quota.Trial && duedate == now)
                    {
                        action = Constants.ActionTariffWarningTrial2;
                        toadmins = true;
                    }

                    #endregion

                    #region 5 days after trial expired

                    else if (quota.Trial && duedate != DateTime.MaxValue && duedate.AddDays(5) == now && tenant.VersionChanged <= tenant.CreatedDateTime)
                    {
                        action = Constants.ActionTariffWarningTrial3;
                        toadmins = true;
                        greenButtonText = WebstudioNotifyPatternResource.ButtonExtendTrialButton;
                        greenButtonUrl = "mailto:sales@onlyoffice.com";
                    }

                    #endregion

                    #region 30 days after trial expired + only 1 user

                    else if (quota.Trial && duedate != DateTime.MaxValue && duedate.AddDays(30) == now && CoreContext.UserManager.GetUsers().Count() == 1)
                    {
                        action = Constants.ActionTariffWarningTrial4;
                        toadmins = true;
                        greenButtonText = WebstudioNotifyPatternResource.ButtonSignUpPersonal;
                        greenButtonUrl = "https://personal.onlyoffice.com";
                    }

                    #endregion

                    #endregion

                    #region Payment warning letters

                    #region 7 days before paid expired

                    else if (tariff.State == TariffState.Paid && duedate != DateTime.MaxValue && duedate.AddDays(-7) == now)
                    {
                        action = (TenantExtra.Enterprise && !defaultRebranding)
                                     ? Constants.ActionPaymentWarningBefore7Whitelabel
                                     : Constants.ActionPaymentWarningBefore7;
                        toadmins = true;
                        greenButtonText = WebstudioNotifyPatternResource.ButtonSelectPricingPlans;
                        greenButtonUrl = CommonLinkUtility.GetFullAbsolutePath("~/tariffs.aspx");
                    }

                    #endregion

                    #region paid expires today

                    else if (tariff.State >= TariffState.Paid && duedate == now)
                    {
                        action = (TenantExtra.Enterprise && !defaultRebranding)
                                     ? Constants.ActionPaymentWarningWhitelabel
                                     : Constants.ActionPaymentWarning;
                        toadmins = true;
                        greenButtonText = WebstudioNotifyPatternResource.ButtonSelectPricingPlans;
                        greenButtonUrl = CommonLinkUtility.GetFullAbsolutePath("~/tariffs.aspx");
                    }

                    #endregion

                    #region 3 days after paid expired on delay NOT ENTERPRISE

                    else if (!TenantExtra.Enterprise && tariff.State == TariffState.Delay && duedate != DateTime.MaxValue && duedate.AddDays(3) == now)
                    {
                        action = Constants.ActionPaymentWarningAfter3;
                        toadmins = true;
                    }

                    #endregion

                    #region payment delay expires today NOT ENTERPRISE

                    else if (!TenantExtra.Enterprise && tariff.State >= TariffState.Delay && delayDuedate == now)
                    {
                        action = Constants.ActionPaymentWarningDelayDue;
                        toadmins = true;
                    }

                    #endregion

                    #endregion

                    #region 5 days after registration without activity in 1 or more days

                    if ((TenantExtra.Enterprise && quota.Free || !quota.Free) && tenant.CreatedDateTime.Date.AddDays(5) == now && defaultRebranding)
                    {

                        List<DateTime> datesWithActivity;
                        var query = new SqlQuery("feed_aggregate")
                            .Select(new SqlExp("cast(created_date as date) as short_date"))

                            .Where("tenant", CoreContext.TenantManager.GetCurrentTenant().TenantId)
                            .Where(Exp.Le("created_date", now.AddDays(-1)))
                            .GroupBy("short_date");

                        using (var db = new DbManager(dbid))
                        {
                            datesWithActivity = db
                                .ExecuteList(query)
                                .ConvertAll(r => Convert.ToDateTime(r[0]));
                        }

                        if (datesWithActivity.Count < 5)
                        {
                            action = Constants.ActionAfterCreation5;
                            toadmins = true;
                        }
                    }

                    #endregion

                    #region 2 weeks after 3 times paid NOT ENTERPRISE

                    if (!TenantExtra.Enterprise)
                    {
                        try
                        {
                            if (!quota.Free && tariff.State == TariffState.Paid)
                            {
                                DateTime lastDatePayment;

                                var query = new SqlQuery("tenants_tariff")
                                    .Select("max(create_on)")
                                    .Where(Exp.Eq("tenant", tenant.TenantId) & Exp.In("tariff", monthQuotasIds))
                                    .Having(Exp.Sql("count(*) >= 3"));

                                using (var db = new DbManager(dbid))
                                {
                                    lastDatePayment = db.ExecuteScalar<DateTime>(query);
                                }

                                if (lastDatePayment != DateTime.MinValue && lastDatePayment.AddDays(14) == now)
                                {
                                    action = Constants.ActionAfterPayment1;
                                    toadmins = true;
                                    footer = "common";
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error(e);
                        }
                    }

                    #endregion


                    if (action != null)
                    {
                        IEnumerable<UserInfo> users = new List<UserInfo>();

                        if (toadmins && touser && toguest)
                        {
                            users = CoreContext.UserManager.GetUsers();
                        }
                        else if (toadmins && !touser && !toguest)
                        {
                            users = CoreContext.UserManager.GetUsersByGroup(ASC.Core.Users.Constants.GroupAdmin.ID);
                        }
                        else if (!toadmins && touser && !toguest)
                        {
                            users = CoreContext.UserManager.GetUsers(EmployeeStatus.Default, EmployeeType.User)
                                .Where(u => !CoreContext.UserManager.IsUserInGroup(u.ID, ASC.Core.Users.Constants.GroupAdmin.ID))
                                .ToArray();
                        }
                        else if (!toadmins && !touser && toguest)
                        {
                            users = CoreContext.UserManager.GetUsers(EmployeeStatus.Default, EmployeeType.Visitor);
                        }
                        else if (toadmins && touser && !toguest)
                        {
                            users = CoreContext.UserManager.GetUsers(EmployeeStatus.Default, EmployeeType.User);
                        }
                        else if (!toadmins && touser && toguest)
                        {
                            users = CoreContext.UserManager.GetUsers()
                                .Where(u => !CoreContext.UserManager.IsUserInGroup(u.ID, ASC.Core.Users.Constants.GroupAdmin.ID))
                                .ToArray();
                        }
                        else if (toadmins && !touser && toguest)
                        {
                            var admins = CoreContext.UserManager.GetUsersByGroup(ASC.Core.Users.Constants.GroupAdmin.ID);
                            var guests = CoreContext.UserManager.GetUsers(EmployeeStatus.Default, EmployeeType.Visitor);

                            users = admins.Concat(guests);
                        }




                        foreach (var u in users)
                        {
                            var culture = string.IsNullOrEmpty(u.CultureName) ? tenant.GetCulture() : u.GetCulture();
                            Thread.CurrentThread.CurrentCulture = culture;
                            Thread.CurrentThread.CurrentUICulture = culture;
                            var rquota = TenantExtra.GetRightQuota();

                            client.SendNoticeToAsync(
                                action,
                                null,
                                new[] { ToRecipient(u.ID) },
                                new[] { EMailSenderName },
                                null,
                                new TagValue(Constants.TagUserName, u.DisplayUserName()),
                                new TagValue(Constants.TagPricingPage, CommonLinkUtility.GetFullAbsolutePath("~/tariffs.aspx")),
                                new TagValue(Constants.TagActiveUsers, CoreContext.UserManager.GetUsers().Count()),
                                new TagValue(Constants.TagPrice, rquota.Price),//TODO: use price partner
                                new TagValue(Constants.TagPricePeriod, rquota.Year3 ? UserControlsCommonResource.TariffPerYear3 : rquota.Year ? UserControlsCommonResource.TariffPerYear : UserControlsCommonResource.TariffPerMonth),
                                new TagValue(Constants.TagDueDate, duedate.ToLongDateString()),
                                new TagValue(Constants.TagDelayDueDate, (delayDuedate != DateTime.MaxValue ? delayDuedate : duedate).ToLongDateString()),
                                Constants.TagBlueButton(WebstudioNotifyPatternResource.ButtonRequestCallButton, "http://www.onlyoffice.com/call-back-form.aspx"),
                                Constants.TagGreenButton(greenButtonText, greenButtonUrl),
                                Constants.TagTableTop(),
                                Constants.TagTableItem(1, tableItemText1, tableItemUrl1, tableItemImg1, tableItemComment1, tableItemLearnMoreText1, tableItemLearnMoreUrl1),
                                Constants.TagTableItem(2, tableItemText2, tableItemUrl2, tableItemImg2, tableItemComment2, tableItemLearnMoreText2, tableItemLearnMoreUrl2),
                                Constants.TagTableItem(3, tableItemText3, tableItemUrl3, tableItemImg3, tableItemComment3, tableItemLearnMoreText3, tableItemLearnMoreUrl3),
                                Constants.TagTableItem(4, tableItemText4, tableItemUrl4, tableItemImg4, tableItemComment4, tableItemLearnMoreText4, tableItemLearnMoreUrl4),
                                Constants.TagTableItem(5, tableItemText5, tableItemUrl5, tableItemImg5, tableItemComment5, tableItemLearnMoreText5, tableItemLearnMoreUrl5),
                                Constants.TagTableBottom(),
                                new TagValue(CommonTags.WithPhoto, string.IsNullOrEmpty(tenant.PartnerId) ? footer : string.Empty));
                        }
                    }
                }
                catch (Exception err)
                {
                    log.Error(err);
                }
            }
            }
            log.Info("End SendTariffWarnings.");
        }

        #region Personal

        public void SendLettersPersonal(DateTime scheduleDate)
        {

            var log = LogManager.GetLogger("ASC.Notify");

            log.Info("Start SendLettersPersonal...");

            foreach (var tenant in CoreContext.TenantManager.GetTenants().Where(t => t.Status == TenantStatus.Active))
            {
                try
                {
                    int sendCount = 0;

                    CoreContext.TenantManager.SetCurrentTenant(tenant.TenantId);

                    log.InfoFormat("Current tenant: {0}", tenant.TenantId);

                    var users = CoreContext.UserManager.GetUsers(EmployeeStatus.Active);


                    foreach (var user in users)
                    {
                        INotifyAction action;

                        SecurityContext.AuthenticateMe(CoreContext.Authentication.GetAccountByID(user.ID));

                        var culture = tenant.GetCulture();
                        if (!string.IsNullOrEmpty(user.CultureName))
                        {
                            try
                            {
                                culture = user.GetCulture();
                            }
                            catch (CultureNotFoundException exception)
                            {

                                log.Error(exception);
                            }
                        }

                        Thread.CurrentThread.CurrentCulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;

                        var dayAfterRegister = (int)scheduleDate.Date.Subtract(user.CreateDate.Date).TotalDays;

                        switch (dayAfterRegister)
                        {
                            case 7:
                                action = Constants.ActionAfterRegistrationPersonal7;
                                break;
                            case 14:
                                action = Constants.ActionAfterRegistrationPersonal14;
                                break;
                            case 21:
                                action = Constants.ActionAfterRegistrationPersonal21;
                                break;
                            default:
                                continue;

                        }

                        log.InfoFormat(@"Send letter personal '{1}'  to {0} culture {2}. tenant id: {3} user culture {4} create on {5} now date {6}",
                              user.Email, action.Name, culture, tenant.TenantId, user.GetCulture(), user.CreateDate, scheduleDate.Date);

                        sendCount++;

                        client.SendNoticeToAsync(
                          action,
                          null,
                          RecipientFromEmail(new[] { user.Email.ToLower() }, true),
                          new[] { EMailSenderName },
                          null,
                          Constants.TagMarkerStart,
                          Constants.TagMarkerEnd,
                          Constants.TagFrameStart,
                          Constants.TagFrameEnd,
                          Constants.TagHeaderStart,
                          Constants.TagHeaderEnd,
                          Constants.TagStrongStart,
                          Constants.TagStrongEnd,
                          Constants.TagSignatureStart,
                          Constants.TagSignatureEnd,
                          new TagValue(CommonTags.WithPhoto, "personal"),
                          new TagValue(CommonTags.IsPromoLetter, "true"));
                    }

                    log.InfoFormat("Total send count: {0}", sendCount);

                }
                catch (Exception err)
                {
                    log.Error(err);
                }
            }

            log.Info("End SendLettersPersonal.");
        }

        public void SendInvitePersonal(string email, string additionalMember = "")
        {
            var newUserInfo = CoreContext.UserManager.GetUserByEmail(email);
            if (!CoreContext.UserManager.UserExists(newUserInfo.ID))
            {
                var confirmUrl = CommonLinkUtility.GetConfirmationUrl(email, ConfirmType.EmpInvite, (int)EmployeeType.User)
                                 + "&emplType=" + (int)EmployeeType.User
                                 + "&lang=" + Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName
                                 + additionalMember;

                client.SendNoticeToAsync(
                            Constants.ActionConfirmationPersonal,
                            null,
                            RecipientFromEmail(new string[] { email }, false),
                            new[] { EMailSenderName },
                            null,
                            new TagValue(Constants.TagInviteLink, confirmUrl),
                            Constants.TagSignatureStart,
                            Constants.TagSignatureEnd,
                            new TagValue(CommonTags.WithPhoto, "personal"),
                            new TagValue(CommonTags.IsPromoLetter, "true"),
                            Constants.UnsubscribeLink,
                            new TagValue(CommonTags.Culture, Thread.CurrentThread.CurrentUICulture.Name));
            }
        }

        public void SendUserWelcomePersonal(UserInfo newUserInfo)
        {
            client.SendNoticeToAsync(
                Constants.ActionAfterRegistrationPersonal1,
                null,
                RecipientFromEmail(new[] { newUserInfo.Email.ToLower() }, true),
                new[] { EMailSenderName },
                null,
                new TagValue(Constants.TagInviteLink, GenerateActivationConfirmUrl(newUserInfo)),
                new TagValue(Constants.TagUserName, newUserInfo.DisplayUserName()),
                Constants.TagMarkerStart,
                Constants.TagMarkerEnd,
                Constants.TagFrameStart,
                Constants.TagFrameEnd,
                Constants.TagHeaderStart,
                Constants.TagHeaderEnd,
                Constants.TagStrongStart,
                Constants.TagStrongEnd,
                Constants.TagSignatureStart,
                Constants.TagSignatureEnd,
                new TagValue(CommonTags.WithPhoto, "personal"),
                new TagValue(CommonTags.IsPromoLetter, "true"),
                Constants.UnsubscribeLink,
                CreateSendFromTag());
        }

        #endregion

        #region Migration Portal

        public void MigrationPortalStart(string region, bool notify)
        {
            MigrationNotify(Constants.ActionMigrationPortalStart, region, string.Empty, notify);
        }

        public void MigrationPortalSuccess(string region, string url, bool notify)
        {
            MigrationNotify(Constants.ActionMigrationPortalSuccess, region, url, notify);
        }

        public void MigrationPortalError(string region, string url, bool notify)
        {
            MigrationNotify(!string.IsNullOrEmpty(region) ? Constants.ActionMigrationPortalError : Constants.ActionMigrationPortalServerFailure, region, url, notify);
        }

        private void MigrationNotify(INotifyAction action, string region, string url, bool notify)
        {
            var users = CoreContext.UserManager.GetUsers()
                .Where(u => notify ? u.ActivationStatus == EmployeeActivationStatus.Activated : u.IsOwner())
                .Select(u => ToRecipient(u.ID));

            if (users.Any())
            {
                client.SendNoticeToAsync(
                    action,
                    null,
                    users.ToArray(),
                    new[] { EMailSenderName },
                    null,
                    new TagValue(Constants.TagRegionName, TransferResourceHelper.GetRegionDescription(region)),
                    new TagValue(Constants.TagPortalUrl, url));
            }
        }

        public void PortalRenameNotify(String oldVirtualRootPath)
        {
            var tenant = CoreContext.TenantManager.GetCurrentTenant();

            var users = CoreContext.UserManager.GetUsers()
                        .Where(u => u.ActivationStatus == EmployeeActivationStatus.Activated);


            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CoreContext.TenantManager.SetCurrentTenant(tenant);

                    foreach (var u in users)
                    {
                        var culture = string.IsNullOrEmpty(u.CultureName) ? tenant.GetCulture() : u.GetCulture();
                        Thread.CurrentThread.CurrentCulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;

                        client.SendNoticeToAsync(
                            Constants.ActionPortalRename,
                            null,
                            new[] { ToRecipient(u.ID) },
                            new[] { EMailSenderName },
                            null,
                            new TagValue(Constants.TagPortalUrl, oldVirtualRootPath),
                            new TagValue(Constants.TagUserDisplayName, u.DisplayUserName()));
                    }
                }
                catch (Exception ex)
                {
                    LogManager.GetLogger("ASC.Notify").Error(ex);
                }
                finally
                {

                }
            });
        }

        #endregion

        #region Helpers

        private IRecipient ToRecipient(Guid userID)
        {
            return source.GetRecipientsProvider().GetRecipient(userID.ToString());
        }

        private IDirectRecipient[] RecipientFromEmail(string[] emails, bool checkActivation)
        {
            return (emails ?? new string[0])
                .Select(e => new DirectRecipient(e, null, new[] { e }, checkActivation))
                .ToArray();
        }

        private static TagValue CreateSendFromTag()
        {
            return new TagValue(CommonTags.SendFrom,
                SecurityContext.IsAuthenticated && SecurityContext.CurrentAccount is IUserAccount ?
                    DisplayUserSettings.GetFullUserName(CoreContext.UserManager.GetUsers(SecurityContext.CurrentAccount.ID), false).Replace(">", "&#62").Replace("<", "&#60") :
                    CoreContext.TenantManager.GetCurrentTenant().Name);
        }

        private string GetMyStaffLink()
        {
            return CommonLinkUtility.GetFullAbsolutePath(CommonLinkUtility.GetMyStaff());
        }

        private string AddHttpToUrl(string url)
        {
            var httpPrefix = Uri.UriSchemeHttp + Uri.SchemeDelimiter;
            return !string.IsNullOrEmpty(url) && !url.StartsWith(httpPrefix) ? httpPrefix + url : url;
        }

        private static string GenerateActivationConfirmUrl(UserInfo user)
        {
            var confirmUrl = CommonLinkUtility.GetConfirmationUrl(user.Email, ConfirmType.Activation);

            return confirmUrl + String.Format("&uid={0}&firstname={1}&lastname={2}",
                                              SecurityContext.CurrentAccount.ID,
                                              HttpUtility.UrlEncode(user.FirstName),
                                              HttpUtility.UrlEncode(user.LastName));
        }

        #endregion
    }
}