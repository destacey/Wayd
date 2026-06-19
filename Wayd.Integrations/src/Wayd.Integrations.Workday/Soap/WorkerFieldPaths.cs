namespace Wayd.Integrations.Workday.Soap;

/// <summary>
/// Centralized XPath expressions for the Worker fields Wayd consumes. Used by both the bulk-sync
/// parser (to extract values) and the init probe (to detect ISSG permission gaps).
///
/// Each constant is an array because Workday occasionally renames a field across major versions —
/// the array carries the current XPath plus any fallbacks for older or pre-announced renames.
/// First non-empty match wins.
/// </summary>
internal static class WorkerFieldPaths
{
    // --- Identity references ---
    public static readonly string[] WorkerWid =
    [
        "wd:Worker_Reference/wd:ID[@wd:type='WID']",
    ];

    public static readonly string[] EmployeeId =
    [
        "wd:Worker_Reference/wd:ID[@wd:type='Employee_ID']",
        "wd:Worker_Reference/wd:ID[@wd:type='Contingent_Worker_ID']",
    ];

    // --- Personal data ---
    // Workday exposes two name blocks: Legal_Name_Data (always populated) and Preferred_Name_Data
    // (populated when the worker has a preferred name; mirrors legal when they don't). The connection
    // setting UsePreferredName decides which block the sync reads from; the projector falls back to
    // legal if the preferred block is missing or empty for a particular worker.
    public static readonly string[] FirstName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Legal_Name_Data/wd:Name_Detail_Data/wd:First_Name",
    ];

    public static readonly string[] MiddleName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Legal_Name_Data/wd:Name_Detail_Data/wd:Middle_Name",
    ];

    public static readonly string[] LastName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Legal_Name_Data/wd:Name_Detail_Data/wd:Last_Name",
    ];

    public static readonly string[] PreferredFirstName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Preferred_Name_Data/wd:Name_Detail_Data/wd:First_Name",
    ];

    public static readonly string[] PreferredMiddleName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Preferred_Name_Data/wd:Name_Detail_Data/wd:Middle_Name",
    ];

    public static readonly string[] PreferredLastName =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Name_Data/wd:Preferred_Name_Data/wd:Name_Detail_Data/wd:Last_Name",
    ];

    // Workday lists multiple email addresses with usage descriptors; "WORK" is the one we want.
    public static readonly string[] WorkEmail =
    [
        "wd:Worker_Data/wd:Personal_Data/wd:Contact_Data/wd:Email_Address_Data[wd:Usage_Data/wd:Type_Data/wd:Type_Reference/wd:ID[@wd:type='Communication_Usage_Type_ID']='WORK']/wd:Email_Address",
        "wd:Worker_Data/wd:Personal_Data/wd:Contact_Data/wd:Email_Address_Data/wd:Email_Address",
    ];

    // The Workday account login. Always exposed by the base Public Worker Reports domain. We only
    // use this as an email source when the admin explicitly opts in via UseUserIdAsEmailFallback —
    // and only when it parses as a real email address.
    public static readonly string[] UserId =
    [
        "wd:Worker_Data/wd:User_ID",
    ];

    // --- Employment status ---
    public static readonly string[] Active =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Status_Data/wd:Active",
    ];

    public static readonly string[] HireDate =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Status_Data/wd:Hire_Date",
    ];

    // --- Job / org ---
    public static readonly string[] JobTitle =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Business_Title",
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Job_Title",
    ];

    // Department is resolved dynamically in WorkdayStaffingService.ResolveDepartment using the
    // admin-configured Organization_Type_ID. The init probe populates the catalog of valid type
    // IDs via Get_Organizations, so admins can pick SUPERVISORY (the default), COST_CENTER,
    // BUSINESS_UNIT, or a tenant-custom type. There's no static XPath here because the filter
    // value is per-connection rather than a single canonical Workday element.

    public static readonly string[] OfficeLocation =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Business_Site_Summary_Data/wd:Name",
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Business_Site_Summary_Data/wd:Location_Reference/wd:Descriptor",
    ];

    // Management chain layout per the v46.1 schema:
    //   Worker_Data
    //     Management_Chain_Data                              (type: Worker_Management_Chain_DataType)
    //       Worker_Supervisory_Management_Chain_Data         (type: Worker_Supervisory_Management_Chain_DataType)
    //         Management_Chain_Data        [0..unbounded]    (one per supervisory-org level)
    //           Manager_Reference          [0..unbounded]    (WorkerObjectType — carries WID + Employee_ID children)
    // The chain is ordered CEO → direct manager, so the *last* Management_Chain_Data entry holds
    // the worker's direct manager. We take the first Manager_Reference inside it (co-managers are
    // possible; conventionally the first listed is the primary).
    // Requires Include_Management_Chain_Data on the request.
    public static readonly string[] ManagerWorkerWid =
    [
        "wd:Worker_Data/wd:Management_Chain_Data/wd:Worker_Supervisory_Management_Chain_Data/wd:Management_Chain_Data[last()]/wd:Manager_Reference[1]/wd:ID[@wd:type='WID']",
    ];

    public static readonly string[] ManagerEmployeeId =
    [
        "wd:Worker_Data/wd:Management_Chain_Data/wd:Worker_Supervisory_Management_Chain_Data/wd:Management_Chain_Data[last()]/wd:Manager_Reference[1]/wd:ID[@wd:type='Employee_ID']",
    ];

    // --- Worker type ---
    // Worker_Type_Reference is a Workday reference element whose Descriptor attribute carries the
    // tenant's display value (e.g. "Regular Employee", "Contingent Worker") and whose child
    // ID[@type='Employee_Type_ID'] carries the stable tenant code. We surface the descriptor (free
    // form, per-tenant) because the whole point is to show users the same label they see in their HRIS.

    /// <summary>Path to the Worker_Type_Reference parent element — the <c>wd:Descriptor</c> attribute carries the display name.</summary>
    public static readonly string[] WorkerTypeReference =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Worker_Type_Reference",
    ];

    /// <summary>Fallback path to the stable tenant code (e.g. <c>Regular_Employee</c>) when the descriptor isn't populated.</summary>
    public static readonly string[] WorkerTypeId =
    [
        "wd:Worker_Data/wd:Employment_Data/wd:Worker_Job_Data/wd:Position_Data/wd:Worker_Type_Reference/wd:ID[@wd:type='Employee_Type_ID']",
    ];
}
