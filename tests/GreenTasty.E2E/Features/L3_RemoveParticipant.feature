@L3
Feature: L3 - Owner removes a participant from the group
  As the Owner of a group reservation
  I want to remove a participant together with their cart
  So that only the right people's dishes go to the restaurant

  Background:
    Given a restaurant with a table for 6 guests
    And a client "Owner" has booked the table for 5 guests

  @ui
  Scenario: Removing one of several participants recalculates the total
    Given guest "Anna" has joined the group
    And "Anna" has added 2 x "Caesar Salad" to the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Grilled Salmon" to the cart
    And guest "Clara" has joined the group
    And "Clara" has added 1 x "Cheesecake" to the cart
    And the Owner opens the group pre-order page
    When the Owner removes participant "Ben" on the page
    Then the page does not show participant "Ben"
    And the page shows participant "Anna" as "Still choosing" with subtotal 25.00
    And the page shows participant "Clara" as "Still choosing" with subtotal 6.50
    And the page shows grand total 31.50

  @ui @smoke @e2e
  Scenario: End to end - removed participant's dishes do not reach the restaurant
    Given the Owner has pre-ordered 1 x "Tomato Soup"
    And guest "Anna" has joined the group
    And "Anna" has added 1 x "Caesar Salad" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 2 x "Grilled Salmon" to the cart
    And "Ben" has submitted the cart
    And the Owner opens the group pre-order page
    When the Owner removes participant "Ben" on the page
    And the Owner submits the pre-order on the page
    Then the page shows that the pre-order was sent to the restaurant
    When the waiter opens the pre-order of the reservation
    Then the waiter sees 1 x "Caesar Salad"
    And the waiter does not see "Grilled Salmon"

  @api
  Scenario: Removing a participant who already submitted the cart
    Given guest "Anna" has joined the group
    And "Anna" has added 1 x "Caesar Salad" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Tomato Soup" to the cart
    When the Owner removes participant "Anna"
    Then the request succeeds
    And the Owner sees 1 participants with total 7.00
    And the Owner does not see participant "Anna"

  @api
  Scenario: Removing the last participant leaves an empty group
    Given guest "Anna" has joined the group
    And "Anna" has added 1 x "Cheesecake" to the cart
    When the Owner removes participant "Anna"
    Then the request succeeds
    And the Owner sees 0 participants with total 0.00

  @api @negative
  Scenario: A participant of another group cannot be removed
    Given guest "Anna" has joined the group
    And another group exists with guest "Zoe"
    When the Owner removes participant "Zoe" from the own group
    Then the request is rejected with status 400
    And the Owner sees 1 participants with total 0.00

  @api @negative
  Scenario: A participant that does not exist
    When the Owner removes a participant that does not exist
    Then the request is rejected with status 404

  @api @security
  Scenario Outline: Only the Owner can remove participants
    Given guest "Anna" has joined the group
    When <caller> removes participant "Anna"
    Then the request is rejected with status <status>
    And the Owner sees 1 participants with total 0.00

    Examples:
      | caller            | status |
      | another client    | 403    |
      | the waiter        | 403    |
      | an anonymous user | 401    |
