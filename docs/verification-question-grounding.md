# Verification question grounding

The live cap regression came from deterministic templates in both Python and ASP.NET:
the presence of `cap` selected a question about a mark underneath it. Category-based
LLM drafting then inherited that unsupported assumption. Validation checked privacy,
IDs and schema, but did not establish that a question was answerable from its source.

The internal path remains ASP.NET `ClaimService` → authenticated
`VerificationAgentClient` → Python `verification_node` → `_build_challenges` →
optional LLM drafting → validation → ASP.NET response validation → persistence.
Students continue to access ASP.NET only; expected values never enter model prompts,
student DTOs, traces or persisted question audits.

`ai/app/agents/verification_grounding.json` is the single question catalogue. Python
loads it locally; ASP.NET embeds the same file as a resource at build time. Each rule
recognises an explicit observation and offers fixed public wording. Colour, cap type,
brand, damage, sticker presence and feature location have separate candidates. Patterns
inspect one clause at a time, split at sentences and conjunctions, so attributes and
locations cannot be moved between independent clauses. Negated and uncertain clauses
are conservatively skipped. Question values are never interpolated from evidence.

After model generation, the question must match a catalogue candidate supported by its
original observation (case, punctuation and colour/color variants are normalised).
The existing private-answer checks also apply. Unsupported nouns, attributes, spatial
relations and compound questions cannot pass by merely using words from the evidence.
Invalid output uses a deterministic grounded question immediately; another model call
is unnecessary. Unrecognised observations use the attribute-free open request
`What identifying detail can you provide about the item?` rather than an invented
location or characteristic. Unsupported but plausible paraphrases are rejected too:
this grammar intentionally favours safe coverage over unrestricted model wording.

For the Nike bottle regression, cap colour, brand, scratch presence/location and sticker
presence/colour/location are accepted. Underneath-cap markings, sticker writing/shape,
contents, hidden compartments, engraving and serial numbers are rejected.

Follow-up drafting uses the same validation and fallback. Drafting and sending must be
grounded in the chosen unused original observation or the newly recorded physical
observation; previous evidence is used only for reuse and privacy checks. Existing
audit evidence keys continue to reserve an entire stored observation after use, so this
fix does not redefine unused-evidence tracking or scoring. Decision and override reasons
retain privacy-only validation, independently of the question grammar.

No database migration, client changes, threshold changes or lifecycle changes are needed.
Rebuild ASP.NET and deploy the updated internal Python service together to use the shared
catalogue at both boundaries. Existing persisted questions are not rewritten.
