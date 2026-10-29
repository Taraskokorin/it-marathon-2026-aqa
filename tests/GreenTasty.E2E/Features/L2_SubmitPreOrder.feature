@L2
Feature: L2 - Owner submits the consolidated pre-order
  As the Owner of a group reservation
  I want to send one consolidated pre-order to the restaurant
  So that the waiter prepares everything the group has chosen

  Background:
    Given a restaurant with a table for 6 guests
    And a client "Owner" has booked the table for 5 guests

  @ui @smoke
  Scenario: Owner submits; identical dishes are merged and unsubmitted carts are ignored
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 2 x "Caesar Salad" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Caesar Salad" to the cart
    And "Ben" has submitted the cart
    And guest "Clara" has joined the group
    And "Clara" has added 1 x "Grilled Salmon" to the cart
    When the Owner opens the group pre-order page
    Then the consolidated pre-order lists 3 x "Caesar Salad"
    And the consolidated pre-order does not list "Grilled Salmon"
    When the Owner submits the pre-order on the page
    Then the page shows that the pre-order was sent to the restaurant
    And the group status is "Submitted"
    And the order has status "SubmittedByCustomer" with 3 x "Caesar Salad" and 1 x "Tomato Soup"
    And the order does not contain "Grilled Salmon"

  @ui
  Scenario: The waiter sees the submitted order with the dishes confirmed by the Owner
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 2 x "Cheesecake" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Cheesecake" to the cart
    And "Ben" has submitted the cart
    And the Owner has submitted the group pre-order
    When the waiter opens the pre-order of the reservation
    Then the waiter sees 3 x "Cheesecake"
    And the waiter sees 1 x "Tomato Soup"

  @api
  Scenario: Submitting creates the restaurant order through the API
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 1 x "Grilled Salmon" to the cart
    And "Anna" has submitted the cart
    When the Owner submits the group pre-order
    Then the request succeeds
    And the group status is "Submitted"
    And the order has status "SubmittedByCustomer" with 1 x "Grilled Salmon" and 1 x "Tomato Soup"

  @api @negative
  Scenario: Cannot submit when no participant has submitted a cart
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 1 x "Caesar Salad" to the cart
    When the Owner submits the group pre-order
    Then the request is rejected with status 400 and message "There are no submitted guest sessions."

  @api @negative
  Scenario: A guest cannot submit an empty cart, so submitted carts always contain dishes
    Given guest "Anna" has joined the group
    When "Anna" submits an empty cart
    Then the request is rejected with status 400

  @api @negative
  Scenario: Cannot submit when the restaurant order does not exist
    Given guest "Anna" has joined the group
    And "Anna" has added 1 x "Caesar Salad" to the cart
    And "Anna" has submitted the cart
    When the Owner submits the group pre-order
    Then the request is rejected with status 400 and message containing "not found"

  @api @negative
  Scenario: Cannot submit a group that does not exist
    When the Owner submits a group pre-order that does not exist
    Then the request is rejected with status 404

  @api @negative
  Scenario: Cannot submit the same group twice
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 1 x "Cheesecake" to the cart
    And "Anna" has submitted the cart
    And the Owner has submitted the group pre-order
    When the Owner submits the group pre-order
    Then the request is rejected with status 400 and message "Group reservation was submitted."

  @api @security
  Scenario Outline: Only the Owner can submit the group pre-order
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 1 x "Cheesecake" to the cart
    And "Anna" has submitted the cart
    When <caller> submits the group pre-order
    Then the request is rejected with status <status>
    And the group status is "Open"

    Examples:
      | caller            | status |
      | another client    | 403    |
      | the waiter        | 403    |
      | an anonymous user | 401    |
