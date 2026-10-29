@L1
Feature: L1 - Owner views the group pre-order
  As the Owner of a group reservation
  I want to see who joined, what each participant ordered and the totals
  So that I know what will be sent to the restaurant

  Background:
    Given a restaurant with a table for 6 guests
    And a client "Owner" has booked the table for 5 guests

  @ui @smoke
  Scenario: Owner sees participants, their dishes, quantities and subtotals
    Given guest "Anna" has joined the group
    And "Anna" has added 2 x "Caesar Salad" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Tomato Soup" to the cart
    And guest "Clara" has joined the group
    When the Owner opens the group pre-order page
    Then the page shows participant "Anna" as "Submitted" with subtotal 25.00
    And participant "Anna" has 2 x "Caesar Salad"
    And the page shows participant "Ben" as "Still choosing" with subtotal 7.00
    And the page shows participant "Clara" as "Still choosing" with subtotal 0.00
    And the page shows grand total 32.00

  @ui
  Scenario: Grand total follows changes in a guest's cart
    Given guest "Anna" has joined the group
    And "Anna" has added 1 x "Grilled Salmon" to the cart
    And the Owner opens the group pre-order page
    And the page shows grand total 24.00
    When "Anna" adds 2 x "Cheesecake" to the cart
    And the Owner refreshes the group pre-order page
    Then the page shows participant "Anna" as "Still choosing" with subtotal 37.00
    And the page shows grand total 37.00

  @api
  Scenario: Total equals the sum of participants and changes with the carts
    Given guest "Anna" has joined the group
    And "Anna" has added 2 x "Caesar Salad" to the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Grilled Salmon" to the cart
    Then the Owner sees 2 participants with total 49.00
    And the total equals the sum of the participants' totals
    When "Ben" removes "Grilled Salmon" from the cart
    Then the Owner sees 2 participants with total 25.00
    And the total equals the sum of the participants' totals

  @api
  Scenario: Participants who have not submitted or have no dishes are listed and marked
    Given guest "Anna" has joined the group
    And "Anna" has added 1 x "Tomato Soup" to the cart
    And "Anna" has submitted the cart
    And guest "Ben" has joined the group
    And "Ben" has added 1 x "Cheesecake" to the cart
    And guest "Clara" has joined the group
    Then the Owner sees participant "Anna" as submitted with total 7.00
    And the Owner sees participant "Ben" as not submitted with total 6.50
    And the Owner sees participant "Clara" as not submitted with total 0.00

  @api
  Scenario: Group that nobody has joined yet
    Then the Owner sees 0 participants with total 0.00

  @api @security
  Scenario Outline: Only the Owner of the group can see the participants
    Given guest "Anna" has joined the group
    When <caller> requests the participants of the group
    Then the request is rejected with status <status>

    Examples:
      | caller           | status |
      | another client   | 403    |
      | the waiter       | 403    |
      | guest "Anna"     | 401    |
      | an anonymous user | 401   |

  @api
  Scenario: Group that does not exist
    When the Owner requests the participants of a group that does not exist
    Then the request is rejected with status 404

  @ui
  Scenario: Owner opens the group pre-order of a reservation that does not exist
    When the Owner opens the group pre-order page of a reservation that does not exist
    Then the page says that the group reservation was not found
