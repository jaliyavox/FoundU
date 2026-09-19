"""Description Parsing Agent module for FoundU AI Service.

Extracts structured item attributes (itemType, primaryColor, secondaryColor, identifyingFeatures)
from natural language descriptions, handles invalid/unclear text, enforces permission boundaries
(no claim approval permissions), and validates outputs before returning.
"""

import re

from app.agents.models import AGENT_PERMISSIONS, AgentName, DescriptionParseResult
from app.agents.state import AgentState

COLOR_TAXONOMY = {
    "black": "Black",
    "grey": "Grey",
    "gray": "Grey",
    "red": "Red",
    "blue": "Blue",
    "green": "Green",
    "yellow": "Yellow",
    "white": "White",
    "brown": "Brown",
    "pink": "Pink",
    "purple": "Purple",
    "orange": "Orange",
    "silver": "Silver",
    "gold": "Gold",
    "navy": "Navy",
    "beige": "Beige",
    "maroon": "Maroon",
    "tan": "Tan",
}

ITEM_TYPE_PATTERNS = [
    (r"\blaptop\s+bag\b", "Laptop Bag"),
    (r"\blaptop\s+case\b", "Laptop Case"),
    (r"\bshoulder\s+bag\b", "Shoulder Bag"),
    (r"\bduffel\s+bag\b", "Duffel Bag"),
    (r"\bwater\s+bottle\b", "Water Bottle"),
    (r"\bid\s+card\b", "ID Card"),
    (r"\bair\s*pods\b", "AirPods"),
    (r"\bear\s*buds\b", "Earbuds"),
    (r"\bbackpack\b", "Backpack"),
    (r"\bhandbag\b", "Handbag"),
    (r"\bpurse\b", "Purse"),
    (r"\bwallet\b", "Wallet"),
    (r"\bphone\b", "Phone"),
    (r"\biphone\b", "iPhone"),
    (r"\blaptop\b", "Laptop"),
    (r"\bmacbook\b", "MacBook"),
    (r"\bkeychain\b", "Keychain"),
    (r"\bkeys?\b", "Keys"),
    (r"\bcalculator\b", "Calculator"),
    (r"\bumbrella\b", "Umbrella"),
    (r"\bheadphones?\b", "Headphones"),
    (r"\bjacket\b", "Jacket"),
    (r"\bcoat\b", "Coat"),
    (r"\bsweater\b", "Sweater"),
    (r"\bwatch\b", "Watch"),
    (r"\bglasses\b", "Glasses"),
    (r"\bsunglasses\b", "Sunglasses"),
    (r"\bnotebook\b", "Notebook"),
    (r"\bbook\b", "Book"),
    (r"\bbag\b", "Bag"),
]


def check_agent_permissions() -> None:
    """Enforce that Description Parsing Agent has NO permission to approve claims."""
    perms = AGENT_PERMISSIONS.get(AgentName.DESCRIPTION_PARSER)
    if not perms or perms.has_approval_permission:
        raise PermissionError(
            "Description Parsing Agent does NOT have permission to approve claims."
        )


def validate_parsed_result(result: DescriptionParseResult) -> DescriptionParseResult:
    """Deterministically validate the parsed output before saving or returning."""
    if not isinstance(result.item_type, str) or not result.item_type.strip():
        raise ValueError("Invalid itemType: must be a non-empty string.")
    if not isinstance(result.primary_color, str) or not result.primary_color.strip():
        raise ValueError("Invalid primaryColor: must be a non-empty string.")
    if not isinstance(result.identifying_features, list):
        raise ValueError("Invalid identifyingFeatures: must be a list of strings.")
    if result.confidence_score < 0.0 or result.confidence_score > 1.0:
        raise ValueError("Invalid confidence_score: must be between 0.0 and 1.0.")
    return result


def parse_item_description(raw_description: str) -> DescriptionParseResult:
    """Parse raw text description into structured DescriptionParseResult contract.

    Input Example: "Black laptop bag, grey zipper, small keychain."
    Output Example:
    {
      "itemType": "Laptop Bag",
      "primaryColor": "Black",
      "secondaryColor": "Grey",
      "identifyingFeatures": ["Small keychain"],
      "is_valid": True,
      "confidence_score": 1.0,
      "unclear_reason": None
    }
    """
    check_agent_permissions()

    if not raw_description or not isinstance(raw_description, str):
        result = DescriptionParseResult(
            item_type="Unknown",
            primary_color="Unknown",
            secondary_color=None,
            identifying_features=[],
            is_valid=False,
            confidence_score=0.0,
            unclear_reason="Description is empty or missing.",
        )
        return validate_parsed_result(result)

    cleaned = raw_description.strip()
    if len(cleaned) < 3 or cleaned.isdigit():
        result = DescriptionParseResult(
            item_type="Unknown",
            primary_color="Unknown",
            secondary_color=None,
            identifying_features=[],
            is_valid=False,
            confidence_score=0.0,
            unclear_reason="Description is too short or numeric.",
        )
        return validate_parsed_result(result)

    # 1. Identify primary item type and matching pattern
    item_type = None
    item_pattern_matched = None
    for pattern, name in ITEM_TYPE_PATTERNS:
        if re.search(pattern, cleaned, re.IGNORECASE):
            item_type = name
            item_pattern_matched = pattern
            break

    # 2. Extract colors mentioned in text in order
    lower_text = cleaned.lower()
    found_colors = []
    color_matches = []
    for color_key, color_name in COLOR_TAXONOMY.items():
        for m in re.finditer(r"\b" + color_key + r"\b", lower_text):
            color_matches.append((m.start(), color_name))

    color_matches.sort(key=lambda x: x[0])
    for _, color_name in color_matches:
        if color_name not in found_colors:
            found_colors.append(color_name)

    primary_color = found_colors[0] if found_colors else None
    secondary_color = found_colors[1] if len(found_colors) > 1 else None

    # 3. Extract identifying features from clauses
    clauses = [c.strip(" .") for c in re.split(r"[,;.]", cleaned) if c.strip(" .")]

    identifying_features = []
    for clause in clauses:
        clause_lower = clause.lower()

        # Skip the primary clause defining item type and primary color (e.g. "Black laptop bag")
        if item_pattern_matched and re.search(item_pattern_matched, clause_lower, re.IGNORECASE):
            continue

        # Skip clause if it serves purely to specify secondary color detail (e.g. "grey zipper")
        if secondary_color and secondary_color.lower() in clause_lower and (
            "zipper" in clause_lower or "strap" in clause_lower or "lining" in clause_lower
        ):
            continue

        formatted_feature = clause[0].upper() + clause[1:] if clause else ""
        if formatted_feature and formatted_feature not in identifying_features:
            identifying_features.append(formatted_feature)

    # Handle unparseable / invalid descriptions
    if not item_type and not primary_color:
        result = DescriptionParseResult(
            item_type="Unknown",
            primary_color="Unknown",
            secondary_color=None,
            identifying_features=[],
            is_valid=False,
            confidence_score=0.1,
            unclear_reason="Description lacks recognizable item type or primary color.",
        )
        return validate_parsed_result(result)

    final_item_type = item_type or "Item"
    final_primary_color = primary_color or "Unspecified"

    confidence = 1.0
    if not item_type or not primary_color:
        confidence = 0.5

    result = DescriptionParseResult(
        item_type=final_item_type,
        primary_color=final_primary_color,
        secondary_color=secondary_color,
        identifying_features=identifying_features,
        is_valid=True,
        confidence_score=confidence,
        unclear_reason=None,
    )
    return validate_parsed_result(result)


def description_parser_node(state: AgentState) -> AgentState:
    """LangGraph node execution function for the Description Parsing Agent."""
    payload = state.get("payload", {})
    description_text = payload.get("description", "")

    # Enforce permission check: reject unauthorized approval attempts
    if payload.get("approve_claim") or payload.get("action") == "approve_claim":
        raise PermissionError(
            "Description Parsing Agent does NOT have permission to approve claims."
        )

    parse_result = parse_item_description(description_text)
    output_dict = parse_result.model_dump(by_alias=True)

    return {
        "output": output_dict,
        "trace": [*state.get("trace", []), "executed:description_parser"],
    }
