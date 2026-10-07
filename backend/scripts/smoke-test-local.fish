#!/usr/bin/env fish

set API_BASE_URL "http://localhost:5091"

function pass
    echo "✅ $argv"
end

function fail
    echo "❌ $argv"
    exit 1
end

function require_command
    if not command -q $argv[1]
        fail "Missing required command: $argv[1]"
    end
end

function assert_success_true
    set LABEL $argv[1]
    set URL $argv[2]
    set TOKEN $argv[3]

    set RESPONSE (curl -s -X GET $URL \
        -H "Authorization: Bearer $TOKEN")

    set SUCCESS (echo $RESPONSE | jq -r '.success')

    if test "$SUCCESS" = "true"
        pass $LABEL
    else
        echo $RESPONSE | jq
        fail "$LABEL failed"
    end
end

function assert_status_code
    set LABEL $argv[1]
    set EXPECTED_STATUS $argv[2]
    set URL $argv[3]
    set TOKEN $argv[4]

    set STATUS_CODE (curl -s -o /tmp/mailarchive-smoke-response.json -w "%{http_code}" \
        -X GET $URL \
        -H "Authorization: Bearer $TOKEN")

    if test "$STATUS_CODE" = "$EXPECTED_STATUS"
        pass "$LABEL returned HTTP $EXPECTED_STATUS"
    else
        cat /tmp/mailarchive-smoke-response.json
        echo
        fail "$LABEL expected HTTP $EXPECTED_STATUS but got HTTP $STATUS_CODE"
    end
end

function login
    set EMAIL $argv[1]
    set PASSWORD $argv[2]

    set RESPONSE (curl -s -X POST "$API_BASE_URL/api/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

    set SUCCESS (echo $RESPONSE | jq -r '.success')

    if test "$SUCCESS" != "true"
        echo $RESPONSE | jq
        fail "Login failed for $EMAIL"
    end

    set TOKEN (echo $RESPONSE | jq -r '.data.accessToken')

    if test -z "$TOKEN" -o "$TOKEN" = "null"
        echo $RESPONSE | jq
        fail "Login returned empty token for $EMAIL"
    end

    echo $TOKEN
end

echo
echo "MailArchive local smoke test"
echo "API: $API_BASE_URL"
echo

require_command curl
require_command jq

echo "Checking API availability..."

set HEALTH_STATUS (curl -s -o /tmp/mailarchive-smoke-health.json -w "%{http_code}" \
    "$API_BASE_URL/swagger/index.html")

if test "$HEALTH_STATUS" != "200"
    fail "API does not look reachable at $API_BASE_URL. Start it first with dotnet run --project MailArchive.API"
end

pass "API reachable"

echo
echo "Logging in..."

set ADMIN_TOKEN (login "admin@example.com" "Admin123!")
pass "Admin login"

set USER_TOKEN (login "user@example.com" "User123!")
pass "User login"

echo
echo "Testing admin dashboard endpoints..."

assert_success_true \
    "Admin dashboard summary" \
    "$API_BASE_URL/api/admin/dashboard/summary" \
    $ADMIN_TOKEN

assert_success_true \
    "Admin dashboard recent activity" \
    "$API_BASE_URL/api/admin/dashboard/recent-activity?take=5" \
    $ADMIN_TOKEN

assert_success_true \
    "Admin dashboard mailbox usage" \
    "$API_BASE_URL/api/admin/dashboard/mailbox-usage?take=10" \
    $ADMIN_TOKEN

assert_success_true \
    "Admin dashboard storage usage" \
    "$API_BASE_URL/api/admin/dashboard/storage-usage" \
    $ADMIN_TOKEN

echo
echo "Testing parser status..."

set PARSER_RESPONSE (curl -s -X GET "$API_BASE_URL/api/imports/parser/status" \
    -H "Authorization: Bearer $ADMIN_TOKEN")

set PARSER_SUCCESS (echo $PARSER_RESPONSE | jq -r '.success')
set ACTIVE_PROVIDER (echo $PARSER_RESPONSE | jq -r '.data.activeProvider')

if test "$PARSER_SUCCESS" = "true"
    pass "Parser status endpoint, active provider: $ACTIVE_PROVIDER"
else
    echo $PARSER_RESPONSE | jq
    fail "Parser status endpoint failed"
end

echo
echo "Testing user dashboard endpoints..."

assert_success_true \
    "User dashboard summary" \
    "$API_BASE_URL/api/me/dashboard/summary" \
    $USER_TOKEN

assert_success_true \
    "User dashboard recent activity" \
    "$API_BASE_URL/api/me/dashboard/recent-activity?take=5" \
    $USER_TOKEN

echo
echo "Testing authorization..."

assert_status_code \
    "Simple user cannot access admin dashboard" \
    "403" \
    "$API_BASE_URL/api/admin/dashboard/summary" \
    $USER_TOKEN

set UNAUTHORIZED_STATUS (curl -s -o /tmp/mailarchive-smoke-unauthorized.json -w "%{http_code}" \
    -X GET "$API_BASE_URL/api/me/dashboard/summary")

if test "$UNAUTHORIZED_STATUS" = "401"
    pass "Missing token returns HTTP 401"
else
    cat /tmp/mailarchive-smoke-unauthorized.json
    echo
    fail "Missing token expected HTTP 401 but got HTTP $UNAUTHORIZED_STATUS"
end

echo
echo "✅ All smoke tests passed."
echo
