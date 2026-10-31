namespace Api.Seed;

public sealed record SeedArticle(string Slug, string Title, string Category, bool Published, string Body);

/// <summary>Fixed help-centre content. The categories match the ticket categories so a ticket can be answered by an article.</summary>
public static class KnowledgeSeedData
{
    public static readonly SeedArticle[] Articles =
    [
        new(
            "reset-your-password",
            "Reset your password",
            "Login & access",
            true,
            """
            Use the reset link from the email we sent you. Reset links expire after 24 hours, so ask for a new one if the link no longer works.

            ## Steps

            1. Open the sign-in page and choose **Forgot password**.
            2. Enter the email address on your account.
            3. Open the link on the same device and browser you will sign in with.
            4. Choose a new password that meets the rules shown under the field.

            If the new password is still rejected, check the rules again. Still stuck? [Open a ticket](/tickets/new) and we will help.
            """),
        new(
            "account-locked-after-failed-sign-ins",
            "Account locked after failed sign-ins",
            "Login & access",
            true,
            """
            After five failed sign-in attempts your account is locked for 30 minutes. The lock lifts on its own, so there is nothing to reset.

            - Wait 30 minutes, then sign in again.
            - If you have forgotten the password, reset it before the next attempt.
            - Ask an admin to unlock the account sooner.
            """),
        new(
            "fix-rejected-authenticator-codes",
            "Fix rejected authenticator codes",
            "Login & access",
            true,
            """
            Codes are rejected when the clock on the device running the authenticator app drifts away from the server. Turn on automatic date and time in the device settings, then request a fresh code.

            ## Still failing?

            Wait for the next 30-second window and try again. Signing in from a private window also rules out a stale session.
            """),
        new(
            "download-an-invoice",
            "Download a copy of an invoice",
            "Billing",
            true,
            """
            Every invoice is listed under **Billing history** in your account. Open the invoice and choose **Download PDF**.

            > Invoices are issued on the first day of each billing period.
            """),
        new(
            "how-long-refunds-take",
            "How long refunds take",
            "Billing",
            true,
            """
            Approved refunds go back to the original payment method. Most banks show the credit within 5 business days, and some take up to 10.

            - Our finance team approves refunds within 2 business days.
            - A refund for a seat reduction is prorated to the day.

            If a refund has not arrived after 10 business days, open a ticket in the **Billing** category and include the invoice number.
            """),
        new(
            "update-billing-details",
            "Update billing email, VAT number and address",
            "Account changes",
            true,
            """
            Admins can change the billing contact, VAT number and billing address from **Account**, then **Billing details**.

            Changes apply to invoices issued after the update. Past invoices keep the details they were issued with.
            """),
        new(
            "add-users-and-change-roles",
            "Add users and change their roles",
            "Account changes",
            true,
            """
            Admins manage people from **Users** in the top bar.

            1. Open **Users**.
            2. Change a person's role to customer, agent or admin.

            Customers only see their own tickets. Agents and admins see the whole queue and can add internal notes.
            """),
        new(
            "export-tickets-to-csv",
            "Export tickets to CSV",
            "How-to",
            true,
            """
            Agents and admins can export the ticket list as a CSV file. The export uses the filters you have set, so filter by status or priority first.

            1. Open **Tickets** and apply the filters you need.
            2. Choose **Export CSV**.

            The file has one row per ticket, with the SLA state at the moment of export. All dates are UTC.
            """),
        new(
            "how-sla-targets-work",
            "How SLA targets work",
            "How-to",
            true,
            """
            Every priority has two targets: a first response and a resolution. The clock starts when the ticket is created.

            - **Urgent:** first response in 1 hour, resolution in 4 hours.
            - **High:** first response in 4 hours, resolution in 24 hours.
            - **Medium:** first response in 8 hours, resolution in 48 hours.
            - **Low:** first response in 24 hours, resolution in 120 hours.

            A ticket is **at risk** once 75% of a target has passed, and **breached** once the target is overdue.
            """),
        new(
            "attachment-size-and-file-types",
            "Attachment size and file types",
            "How-to",
            true,
            """
            Each attachment can be up to 10 MB. Larger files fail to upload, so compress screen recordings or share them by link instead.

            PNG, JPG, PDF and MP4 files are supported. Images and PDFs show a preview in the thread.
            """),
        new(
            "fix-slack-notifications",
            "Fix Slack notifications that stopped",
            "Integrations",
            true,
            """
            If new ticket alerts stop reaching your Slack channel, work through these checks in order.

            1. Make sure the TeamDesk app is still installed in the workspace.
            2. Re-authorise the integration from **Integrations** if its token has expired.
            3. Confirm the channel still exists and TeamDesk has been invited to it.

            Alerts are sent when a ticket is created and when a customer replies.
            """),
        new(
            "api-rate-limits",
            "API rate limits",
            "Integrations",
            true,
            """
            The API allows 300 requests per minute for each key. Requests over the limit get `429 Too Many Requests` with a `Retry-After` header.

            For bulk imports, batch your calls and wait for the number of seconds in `Retry-After` before retrying. Ask us to raise the limit if your import needs more.
            """),
        new(
            "webhook-payload-changes-draft",
            "Webhook payload changes (draft)",
            "Integrations",
            false,
            """
            Draft: the payload changes planned for the next release. Not published yet.
            """),
    ];

    private static readonly string[] RatingComments =
    [
        "Quick and clear, thank you.",
        "Solved it, though it took a while to get a reply.",
        "The agent was patient and explained each step.",
        "Works now. Thanks!",
    ];

    /// <summary>A score for a resolved ticket, weighted towards 4 and 5 the way real satisfaction usually is.</summary>
    public static int ScoreFor(int ticketNumber) => (ticketNumber % 10) switch
    {
        0 => 1,
        1 => 2,
        2 or 3 => 3,
        4 => 4,
        _ => 5,
    };

    /// <summary>The comment left with a rating, or null when the customer only gave a score.</summary>
    public static string? CommentFor(int ticketNumber) =>
        ticketNumber % 3 == 0 ? null : RatingComments[ticketNumber % RatingComments.Length];

    /// <summary>Most resolved tickets are rated; the rest were never answered, which is also true of real queues.</summary>
    public static bool IsRated(int ticketNumber) => ticketNumber % 4 != 0;
}
