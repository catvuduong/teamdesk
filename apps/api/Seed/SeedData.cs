using Api.Auth;

namespace Api.Seed;

public sealed record SeedUser(string Name, string Email, string Role);

public sealed record SeedTicket(string Title, string Description, string Category, string Priority, string Status);

/// <summary>Fixed demo content. Every account uses the same demo password, which is shown on the login page.</summary>
public static class SeedData
{
    public const string DemoPassword = "teamdesk-demo";

    public static readonly SeedUser[] Users =
    [
        new("Dana Whitfield", "admin@teamdesk.demo", Roles.Admin),
        new("Priya Shah", "agent@teamdesk.demo", Roles.Agent),
        new("Marcus Lee", "marcus.lee@teamdesk.demo", Roles.Agent),
        new("Elena Novak", "elena.novak@teamdesk.demo", Roles.Agent),
        new("Harper Quinn", "customer@teamdesk.demo", Roles.Customer),
        new("Leo Martins", "leo.martins@teamdesk.demo", Roles.Customer),
        new("Aisha Rahman", "aisha.rahman@teamdesk.demo", Roles.Customer),
        new("Noah Brennan", "noah.brennan@teamdesk.demo", Roles.Customer),
        new("Mei Lin", "mei.lin@teamdesk.demo", Roles.Customer),
        new("Owen Gallagher", "owen.gallagher@teamdesk.demo", Roles.Customer),
    ];

    public static readonly string[] AgentReplies =
    [
        "Thanks for the details. I can reproduce this on my side and am looking into it now.",
        "Sorry about the trouble. I have checked your account and will update you within the hour.",
        "Good catch. I have logged this with the engineering team and linked your ticket to it.",
        "Thanks for the screenshot, that helps a lot. Could you try once more in a private window?",
        "I have made a change on our side. Please let me know if it still happens after a refresh.",
    ];

    public static readonly string[] InternalNotes =
    [
        "Checked the logs: repeated 401s from the same client. Suspect a stale token cache.",
        "Billing confirms a double invoice run on the 1st. Refund needs finance approval.",
        "Looks related to the webhook retry bug. Waiting on the fix in the next release.",
        "Customer is on the legacy plan, so the feature is not available without an upgrade.",
    ];

    public static readonly string[] CustomerFollowUps =
    [
        "Thanks, any update on this?",
        "Still seeing it this morning, same as before.",
        "That worked, thank you!",
    ];

    public static readonly string[] ResolutionMessages =
    [
        "This is fixed now. I have confirmed the change on our side. Let me know if anything looks off.",
        "Resolved. The refund has been issued and should appear within 5 business days.",
        "Done. The account has been updated and the setting is applied to all users.",
    ];

    public static readonly SeedTicket[] Tickets =
    [
        new("Cannot log in after password reset", "Reset link worked, but the new password is rejected with 'invalid credentials' on the sign-in page. Tried in two browsers.", "Login & access", "high", "open"),
        new("Invoice shows wrong seat count", "Our March invoice bills 14 seats but we only have 11 active users. Please check the license sync.", "Billing", "high", "in_progress"),
        new("CSV export drops the last column", "Exporting tickets to CSV omits the 'resolved at' column when filters are applied.", "Bug report", "medium", "in_progress"),
        new("Add dark mode to the mobile app", "Several agents work night shifts. A dark theme would reduce eye strain on phones.", "Feature request", "low", "open"),
        new("How do I merge two customer accounts?", "We accidentally created two accounts for the same person. What is the process to merge them?", "How-to", "low", "resolved"),
        new("2FA codes rejected on iPad", "Authenticator codes say invalid even though the time on the device is synced. Happens only on the iPad.", "Login & access", "urgent", "in_progress"),
        new("Refund not received after cancellation", "Cancelled the annual plan on the 3rd. The refund still has not appeared in our card statement.", "Billing", "high", "waiting"),
        new("Attachments fail above 10 MB", "Uploading a 14 MB screen recording shows 'upload failed' with no further detail.", "Bug report", "medium", "open"),
        new("Slack integration stopped posting", "New ticket notifications stopped reaching the support channel yesterday around 16:00 UTC.", "Integrations", "urgent", "in_progress"),
        new("Change billing email address", "Our finance contact has changed. Please update the billing email on the account.", "Account changes", "low", "closed"),
        new("Search ignores ticket descriptions", "Searching for a keyword that only appears in the description returns no results.", "Bug report", "medium", "resolved"),
        new("Request SSO via Okta", "We would like to sign in with Okta SAML. Is this on the roadmap, and is there a beta?", "Feature request", "medium", "waiting"),
        new("Locked out after five failed attempts", "Our account locked for 30 minutes after a typo. Could we lower the lockout or add a self-unlock?", "Login & access", "medium", "closed"),
        new("Duplicate charge on invoice INV-2291", "Invoice INV-2291 appears twice in our billing history with the same amount and date.", "Billing", "urgent", "resolved"),
        new("How to set up SLA reminders", "Where do I configure a first-response target so agents get reminded before it breaches?", "How-to", "low", "in_progress"),
        new("Dashboard chart is empty on Monday", "The 14-day chart shows no bars even though tickets were created all weekend.", "Bug report", "low", "open"),
        new("Update company name on the account", "We rebranded to Northwind Logistics. Please update the company name on invoices and the portal.", "Account changes", "low", "resolved"),
        new("Request bulk ticket close", "Could agents close several tickets at once from the list view? We have a backlog of stale tickets.", "Feature request", "low", "open"),
        new("Email notifications arrive twice", "Every reply triggers two emails, one from the portal and one from our mail relay.", "Bug report", "medium", "waiting"),
        new("Cannot add a second admin", "The Add user button is greyed out for our second admin account.", "Account changes", "high", "in_progress"),
        new("Refund request for seat downgrade", "We reduced seats from 40 to 25 mid-cycle. Can we receive a prorated credit?", "Billing", "medium", "closed"),
        new("Webhook payload missing requester email", "The ticket.created webhook no longer includes the requester email in the payload.", "Integrations", "high", "resolved"),
        new("Session expires every 10 minutes", "Since Tuesday we get logged out after ten minutes of inactivity, even while typing a reply.", "Login & access", "high", "in_progress"),
        new("Keyboard shortcut to jump to search", "Pressing slash should focus the search box, like in other support tools.", "Feature request", "low", "closed"),
        new("Tickets stuck in waiting status", "Tickets do not return to open after the customer replies. We have to reopen them manually.", "Bug report", "high", "open"),
        new("Set up a role for contractors", "We need a read-only role for contractors who should see tickets but not internal notes.", "How-to", "medium", "waiting"),
        new("Payment method update fails", "Adding a new corporate card returns 'card declined' although the bank confirms the card is active.", "Billing", "high", "resolved"),
        new("Timestamps show the wrong timezone", "Timestamps show UTC but our team is on Berlin time. Can we pick a display timezone?", "Feature request", "medium", "open"),
        new("Export audit log for compliance", "Our auditors need the full event trail for Q3 as CSV. Is there an export?", "How-to", "high", "in_progress"),
        new("Mobile layout hides the reply button", "On a small Android phone the Send button is hidden below the composer.", "Bug report", "medium", "resolved"),
        new("Former contractor still has access", "A contractor who left in August can still sign in. Please disable their account.", "Account changes", "urgent", "closed"),
        new("Need a sandbox environment", "We want to test automation rules without touching production tickets. Is a sandbox available?", "How-to", "low", "waiting"),
        new("Password policy requirements unclear", "The sign-up form rejects my password but does not say which rule failed.", "Login & access", "low", "resolved"),
        new("Attachment preview blank for PDFs", "PDF attachments show a blank preview in the thread. Downloading the file works.", "Bug report", "medium", "in_progress"),
        new("API rate limit hit during import", "Our nightly import script gets 429 errors after about 200 requests. Can we get a higher limit?", "Integrations", "high", "open"),
        new("Annual plan billing question", "Is the annual plan billed upfront, or can we pay quarterly without losing the discount?", "Billing", "low", "resolved"),
        new("Canned reply for password resets", "Could we save a canned reply for password reset instructions and reuse it across tickets?", "Feature request", "low", "closed"),
        new("Cannot reopen a closed ticket", "A customer replied to a closed ticket and we cannot reopen it from the detail page.", "Bug report", "high", "open"),
        new("Mentions in notes do not notify", "Internal notes mention a colleague but they do not get an email notification.", "Bug report", "medium", "waiting"),
        new("Login loop on Firefox ESR", "After signing in on Firefox ESR, the page redirects back to the login screen endlessly.", "Login & access", "urgent", "in_progress"),
        new("Invoice address update", "Please change the VAT number and billing address on our account for future invoices.", "Billing", "low", "closed"),
        new("Priority change not saved", "I changed the priority to urgent but after a refresh it shows medium again.", "Bug report", "high", "resolved"),
        new("Add custom fields to the ticket form", "We need an order number field on the new ticket form for e-commerce returns.", "Feature request", "medium", "open"),
        new("How to link related tickets", "Is there a way to link duplicate tickets so customers see one thread?", "How-to", "low", "in_progress"),
        new("Integration with Jira", "We would like new tickets to create Jira issues automatically. Is a Jira connector planned?", "Integrations", "medium", "waiting"),
    ];
}
