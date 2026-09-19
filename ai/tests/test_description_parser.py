"""Tests specifically for the Description-Parsing Agent."""

import pytest
from pydantic import ValidationError

from app.agents.description_parser import (
    check_agent_permissions,
    parse_item_description,
    validate_parsed_result,
)
from app.agents.models import (
    AGENT_PERMISSIONS,
    AgentName,
    DescriptionParseRequest,
    DescriptionParseResult,
)


def test_parse_exact_example_description():
    """Verify exact example from prompt specification:

    Input: "Black laptop bag, grey zipper, small keychain."
    Structured output:
    {
      "itemType": "Laptop Bag",
      "primaryColor": "Black",
      "secondaryColor": "Grey",
      "identifyingFeatures": ["Small keychain"]
    }
    """
    raw_input = "Black laptop bag, grey zipper, small keychain."
    result = parse_item_description(raw_input)

    assert isinstance(result, DescriptionParseResult)
    assert result.is_valid is True
    assert result.item_type == "Laptop Bag"
    assert result.primary_color == "Black"
    assert result.secondary_color == "Grey"
    assert result.identifying_features == ["Small keychain"]
    assert result.confidence_score == 1.0

    serialized = result.model_dump(by_alias=True)
    assert serialized["itemType"] == "Laptop Bag"
    assert serialized["primaryColor"] == "Black"
    assert serialized["secondaryColor"] == "Grey"
    assert serialized["identifyingFeatures"] == ["Small keychain"]


def test_parse_backpack_description():
    raw_input = "Red Nike backpack, white logo"
    result = parse_item_description(raw_input)

    assert result.is_valid is True
    assert result.item_type == "Backpack"
    assert result.primary_color == "Red"
    assert result.secondary_color == "White"
    assert "White logo" in result.identifying_features


def test_handle_invalid_empty_description():
    result = parse_item_description("")

    assert result.is_valid is False
    assert result.item_type == "Unknown"
    assert result.primary_color == "Unknown"
    assert result.secondary_color is None
    assert result.identifying_features == []
    assert result.confidence_score == 0.0
    assert result.unclear_reason == "Description is empty or missing."


def test_handle_invalid_gibberish_description():
    result = parse_item_description("asdfghjkl 12345")

    assert result.is_valid is False
    assert result.item_type == "Unknown"
    assert result.primary_color == "Unknown"
    assert result.confidence_score == 0.1
    assert result.unclear_reason == "Description lacks recognizable item type or primary color."


def test_agent_has_no_permission_to_approve_claims():
    """Verify agent has no approval permissions."""
    perms = AGENT_PERMISSIONS[AgentName.DESCRIPTION_PARSER]
    assert perms.has_approval_permission is False
    assert perms.allow_listed_tools == []

    check_agent_permissions()  # Should not raise when configured properly

    perms.has_approval_permission = True
    try:
        with pytest.raises(PermissionError, match="does NOT have permission to approve claims"):
            check_agent_permissions()
    finally:
        perms.has_approval_permission = False  # Reset


def test_output_validation_before_saving():
    """Verify deterministic output validation catches invalid schema values."""
    valid_res = DescriptionParseResult(
        item_type="Laptop Bag",
        primary_color="Black",
        secondary_color="Grey",
        identifying_features=["Small keychain"],
        is_valid=True,
        confidence_score=1.0,
    )
    validated = validate_parsed_result(valid_res)
    assert validated.item_type == "Laptop Bag"

    with pytest.raises(ValidationError):
        DescriptionParseResult(
            item_type="Laptop Bag",
            primary_color="Black",
            confidence_score=2.5,  # Invalid confidence > 1.0
        )


def test_description_parse_request_contract():
    req = DescriptionParseRequest(description="Blue water bottle")
    assert req.description == "Blue water bottle"

    with pytest.raises(ValueError):
        DescriptionParseRequest(description="")
