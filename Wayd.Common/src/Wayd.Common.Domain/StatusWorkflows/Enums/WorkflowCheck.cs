namespace Wayd.Common.Domain.StatusWorkflows.Enums;

/// <summary>
/// One of the checks the engine runs before a workflow is published or put to use. How each applies to an
/// owned and to a managed workflow is declared in <see cref="WorkflowCheckRules"/>.
/// </summary>
public enum WorkflowCheck
{
    /// <summary>At publish: a status in every category the owner type requires.</summary>
    RequiredCategories = 1,

    /// <summary>At publish: a status carrying every alias the owner type requires.</summary>
    RequiredAliases = 2,

    /// <summary>At publish: every alias sits in the category its owner type ties it to, and is one it declares.</summary>
    AliasCategories = 3,

    /// <summary>At publish: a restricted transition set the owner type supports and no record can be stranded by.</summary>
    Transitions = 4,

    /// <summary>At assignment: only a published workflow can be assigned or be part of a scheme.</summary>
    Published = 5,

    /// <summary>At reassignment: every status that records hold is mapped to one in the workflow they move to.</summary>
    CompleteRemap = 6,
}
