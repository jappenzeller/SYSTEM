#!/bin/bash
# Deploy-SpacetimeDB.sh - Unified SpacetimeDB Deployment System for Unix/Linux/macOS
# Comprehensive deployment script for local, test, and production environments

# Default values
ENVIRONMENT="test"
DELETE_DATA=false
INVALIDATE_CACHE=false
PUBLISH_ONLY=false
VERIFY=false
BUILD_CONFIG=false
YES=false
SKIP_BUILD=false
MODULE_PATH="./SYSTEM-server"
LOG_PATH="./Logs/deployment"
TIMEOUT=300

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
WHITE='\033[1;37m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Deployment start time
DEPLOYMENT_START=$(date +%s)
LOG_FILE="${LOG_PATH}/deployment_$(date +%Y%m%d_%H%M%S).log"
ERROR_COUNT=0
WARNING_COUNT=0

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --delete-data)
            DELETE_DATA=true
            shift
            ;;
        --invalidate-cache)
            INVALIDATE_CACHE=true
            shift
            ;;
        --publish-only)
            PUBLISH_ONLY=true
            shift
            ;;
        --verify)
            VERIFY=true
            shift
            ;;
        --build-config)
            BUILD_CONFIG=true
            shift
            ;;
        --yes)
            YES=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --module-path)
            MODULE_PATH="$2"
            shift 2
            ;;
        --help)
            show_help
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

# Environment configuration
declare -A ENV_SERVERS=(
    ["local"]="127.0.0.1:3000"
    ["test"]="https://maincloud.spacetimedb.com"
    ["production"]="https://maincloud.spacetimedb.com"
)

declare -A ENV_MODULES=(
    ["local"]="system"
    ["test"]="system-test"
    ["production"]="system"
)

declare -A ENV_CLOUDFRONT=(
    ["local"]=""
    ["test"]="ENIM1XA5ZCZOT"
    ["production"]="E3HQWKXYZ9MNOP"
)

# Ensure log directory exists
mkdir -p "$LOG_PATH"

# Logging functions
write_log() {
    local level="$1"
    local message="$2"
    local timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    local log_message="[$timestamp] [$level] $message"
    
    # Write to log file
    echo "$log_message" >> "$LOG_FILE"
    
    # Write to console with color
    case "$level" in
        ERROR)
            echo -e "${RED}$log_message${NC}"
            ((ERROR_COUNT++))
            ;;
        WARNING)
            echo -e "${YELLOW}$log_message${NC}"
            ((WARNING_COUNT++))
            ;;
        SUCCESS)
            echo -e "${GREEN}$log_message${NC}"
            ;;
        DEBUG)
            echo -e "${GRAY}$log_message${NC}"
            ;;
        *)
            echo "$log_message"
            ;;
    esac
}

show_help() {
    cat << EOF
SpacetimeDB Unified Deployment System

Usage: $0 [OPTIONS]

Options:
    --environment [local|test|production]  Target environment (default: test)
    --delete-data                          Complete database wipe
    --invalidate-cache                     Clear CloudFront cache
    --publish-only                         Deploy module without data operations
    --verify                               Post-deployment verification
    --build-config                         Generate build-config.json
    --yes                                  Non-interactive mode
    --skip-build                           Skip module compilation
    --module-path PATH                     Path to module (default: ./SYSTEM-server)
    --help                                 Show this help message

Examples:
    # Deploy to test environment
    $0 --environment test

    # Deploy to production with data reset
    $0 --environment production --delete-data --yes

    # Deploy with verification and cache invalidation
    $0 --environment test --verify --invalidate-cache

EOF
}

show_banner() {
    echo
    echo -e "${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║       SpacetimeDB Unified Deployment System v2.0            ║${NC}"
    echo -e "${CYAN}║                   SYSTEM Game Server                        ║${NC}"
    echo -e "${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
    echo
    write_log "INFO" "Starting deployment to $ENVIRONMENT environment"
    write_log "DEBUG" "Server: ${ENV_SERVERS[$ENVIRONMENT]}, Module: ${ENV_MODULES[$ENVIRONMENT]}"
}

test_prerequisites() {
    write_log "INFO" "Checking prerequisites..."
    
    # Check Rust/Cargo
    if ! command -v cargo &> /dev/null; then
        write_log "ERROR" "Cargo not found. Please install Rust."
        return 1
    fi
    cargo_version=$(cargo --version)
    write_log "SUCCESS" "Found $cargo_version"
    
    # Check SpacetimeDB CLI
    if ! command -v spacetime &> /dev/null; then
        write_log "ERROR" "SpacetimeDB CLI not found. Please install from https://spacetimedb.com"
        return 1
    fi
    st_version=$(spacetime version 2>&1)
    write_log "SUCCESS" "Found SpacetimeDB CLI: $st_version"
    
    # Check module path
    if [ ! -d "$MODULE_PATH" ]; then
        write_log "ERROR" "Module path not found: $MODULE_PATH"
        return 1
    fi
    
    # Check for Cargo.toml
    if [ ! -f "$MODULE_PATH/Cargo.toml" ]; then
        write_log "ERROR" "Cargo.toml not found in $MODULE_PATH"
        return 1
    fi
    
    return 0
}

build_module() {
    if [ "$SKIP_BUILD" = true ]; then
        write_log "INFO" "Skipping module build (--skip-build flag set)"
        return 0
    fi
    
    write_log "INFO" "Building Rust module..."
    
    cd "$MODULE_PATH" || return 1
    
    # Clean previous build
    write_log "INFO" "Cleaning previous build artifacts..."
    cargo clean 2>&1 > /dev/null
    
    # Build release version
    write_log "INFO" "Compiling release build..."
    if ! cargo build --release; then
        write_log "ERROR" "Build failed"
        cd - > /dev/null
        return 1
    fi
    
    write_log "SUCCESS" "Module built successfully"
    
    # Generate C# bindings
    write_log "INFO" "Generating C# bindings..."
    if spacetime generate --lang cs --out-dir ../SYSTEM-client-3d/Assets/Scripts/autogen; then
        write_log "SUCCESS" "C# bindings generated successfully"
    else
        write_log "WARNING" "Failed to generate bindings"
    fi
    
    cd - > /dev/null
    return 0
}

get_database_state() {
    local env="$1"
    local server="${ENV_SERVERS[$env]}"
    local module="${ENV_MODULES[$env]}"
    
    write_log "INFO" "Checking current database state..."
    
    # Try to get module info
    if spacetime info "$module" --server "$server" 2>&1 > /dev/null; then
        write_log "INFO" "Module exists: $module"
        
        # Try to count tables
        if spacetime sql "$module" --server "$server" "SELECT COUNT(*) FROM Player" 2>&1 > /dev/null; then
            write_log "INFO" "Database contains player data"
            echo "exists_with_data"
        else
            echo "exists_empty"
        fi
    else
        echo "not_exists"
    fi
}

publish_module() {
    local env="$1"
    local clear_data="$2"
    local server="${ENV_SERVERS[$env]}"
    local module="${ENV_MODULES[$env]}"
    
    write_log "INFO" "Publishing to $env ($server)..."
    
    cd "$MODULE_PATH" || return 1
    
    # Build publish command
    local publish_cmd="spacetime publish"
    
    if [ "$clear_data" = true ]; then
        publish_cmd="$publish_cmd -c"
        write_log "WARNING" "WARNING: Clearing all database data!"
    fi
    
    if [ "$PUBLISH_ONLY" = true ]; then
        write_log "INFO" "Publishing module only (no data operations)"
    fi
    
    publish_cmd="$publish_cmd --server $server $module"
    
    # Execute publish
    write_log "DEBUG" "Executing: $publish_cmd"
    if eval "$publish_cmd"; then
        write_log "SUCCESS" "Module published successfully"
        cd - > /dev/null
        return 0
    else
        write_log "ERROR" "Publish failed"
        cd - > /dev/null
        return 1
    fi
}

invalidate_cloudfront() {
    local distribution_id="$1"
    
    if [ -z "$distribution_id" ]; then
        write_log "INFO" "No CloudFront distribution configured for this environment"
        return 0
    fi
    
    write_log "INFO" "Invalidating CloudFront cache (Distribution: $distribution_id)..."
    
    # Check AWS CLI
    if ! command -v aws &> /dev/null; then
        write_log "WARNING" "AWS CLI not available"
        return 1
    fi
    
    # Create invalidation
    if invalidation=$(aws cloudfront create-invalidation \
        --distribution-id "$distribution_id" \
        --paths "/*" \
        --output json 2>&1); then
        
        invalidation_id=$(echo "$invalidation" | grep -o '"Id": "[^"]*' | grep -o '[^"]*$')
        write_log "SUCCESS" "CloudFront invalidation created: $invalidation_id"
        return 0
    else
        write_log "WARNING" "CloudFront invalidation failed"
        return 1
    fi
}

update_build_config() {
    local env="$1"
    
    if [ "$BUILD_CONFIG" != true ]; then
        return 0
    fi
    
    write_log "INFO" "Updating build configuration for WebGL..."
    
    local server="${ENV_SERVERS[$env]}"
    local module="${ENV_MODULES[$env]}"
    local config_path="./SYSTEM-client-3d/Assets/StreamingAssets/build-config.json"
    
    # Ensure StreamingAssets directory exists
    mkdir -p "$(dirname "$config_path")"
    
    # Create JSON config
    cat > "$config_path" << EOF
{
    "environment": "$env",
    "serverUrl": "$server",
    "moduleName": "$module",
    "buildTime": "$(date '+%Y-%m-%d %H:%M:%S')",
    "version": "2.0.0"
}
EOF
    
    write_log "SUCCESS" "Build configuration updated: $config_path"
    return 0
}

verify_deployment() {
    local env="$1"
    
    if [ "$VERIFY" != true ]; then
        return 0
    fi
    
    write_log "INFO" "Verifying deployment..."
    
    local server="${ENV_SERVERS[$env]}"
    local module="${ENV_MODULES[$env]}"
    local verification_passed=true
    
    # Test 1: Module info
    write_log "INFO" "Test 1: Checking module info..."
    if spacetime info "$module" --server "$server" 2>&1 > /dev/null; then
        write_log "SUCCESS" "Module info retrieved successfully"
    else
        write_log "ERROR" "Failed to retrieve module info"
        verification_passed=false
    fi
    
    # Test 2: Table existence
    write_log "INFO" "Test 2: Verifying table structure..."
    local tables=("Player" "World" "Orb" "WavePacket" "Crystal")
    
    for table in "${tables[@]}"; do
        if spacetime sql "$module" --server "$server" "SELECT COUNT(*) FROM $table" 2>&1 > /dev/null; then
            write_log "SUCCESS" "Table $table verified"
        else
            write_log "WARNING" "Table $table not accessible"
        fi
    done
    
    # Test 3: Connection test
    if [ "$env" != "local" ]; then
        write_log "INFO" "Test 3: Testing connection latency..."
        local start_time=$(date +%s%N)
        
        if spacetime sql "$module" --server "$server" "SELECT 1" 2>&1 > /dev/null; then
            local end_time=$(date +%s%N)
            local ping_time=$(( (end_time - start_time) / 1000000 ))
            write_log "SUCCESS" "Connection test passed (${ping_time}ms)"
        else
            write_log "ERROR" "Connection test failed"
            verification_passed=false
        fi
    fi
    
    # Run SQL verification queries if available
    local verify_script="./Scripts/post-deploy-verify.sql"
    if [ -f "$verify_script" ]; then
        write_log "INFO" "Running verification queries from $verify_script..."
        
        while IFS= read -r query; do
            # Skip comments and empty lines
            if [[ "$query" =~ ^-- ]] || [ -z "$query" ]; then
                continue
            fi
            
            write_log "DEBUG" "Executing: ${query:0:50}..."
            if spacetime sql "$module" --server "$server" "$query" 2>&1 > /dev/null; then
                write_log "SUCCESS" "Query executed successfully"
            else
                write_log "WARNING" "Query failed"
            fi
        done < "$verify_script"
    fi
    
    if [ "$verification_passed" = true ]; then
        return 0
    else
        return 1
    fi
}

confirm_action() {
    local message="$1"
    
    if [ "$YES" = true ]; then
        write_log "INFO" "Auto-confirmed: $message"
        return 0
    fi
    
    echo
    echo -e "${YELLOW}$message${NC}"
    echo -en "${CYAN}Continue? (Y/N): ${NC}"
    
    read -r response
    if [ "$response" = "Y" ] || [ "$response" = "y" ]; then
        return 0
    else
        return 1
    fi
}

show_summary() {
    local duration=$(($(date +%s) - DEPLOYMENT_START))
    
    echo
    echo -e "${CYAN}╔══════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║                    Deployment Summary                       ║${NC}"
    echo -e "${CYAN}╚══════════════════════════════════════════════════════════════╝${NC}"
    
    echo -e "${WHITE}Environment:      $ENVIRONMENT${NC}"
    echo -e "${WHITE}Duration:         $duration seconds${NC}"
    
    if [ $ERROR_COUNT -gt 0 ]; then
        echo -e "${RED}Errors:           $ERROR_COUNT${NC}"
    else
        echo -e "${GREEN}Errors:           $ERROR_COUNT${NC}"
    fi
    
    if [ $WARNING_COUNT -gt 0 ]; then
        echo -e "${YELLOW}Warnings:         $WARNING_COUNT${NC}"
    else
        echo -e "${GREEN}Warnings:         $WARNING_COUNT${NC}"
    fi
    
    echo -e "${GRAY}Log File:         $LOG_FILE${NC}"
    
    if [ $ERROR_COUNT -eq 0 ]; then
        echo
        echo -e "${GREEN}DEPLOYMENT SUCCESSFUL!${NC}"
        
        echo
        echo -e "${CYAN}Connection Details:${NC}"
        echo -e "${WHITE}  Server:  ${ENV_SERVERS[$ENVIRONMENT]}${NC}"
        echo -e "${WHITE}  Module:  ${ENV_MODULES[$ENVIRONMENT]}${NC}"
        
        if [ "$ENVIRONMENT" = "local" ]; then
            echo
            echo -e "${YELLOW}Local server command:${NC}"
            echo -e "${WHITE}  spacetime start${NC}"
        fi
    else
        echo
        echo -e "${RED}DEPLOYMENT FAILED!${NC}"
        echo -e "${YELLOW}Check the log file for details: $LOG_FILE${NC}"
    fi
}

# Main deployment flow
main() {
    show_banner
    
    # Step 1: Prerequisites
    if ! test_prerequisites; then
        write_log "ERROR" "Prerequisites check failed"
        show_summary
        exit 1
    fi
    
    # Step 2: Check database state
    db_state=$(get_database_state "$ENVIRONMENT")
    write_log "INFO" "Database state: $db_state"
    
    # Step 3: Confirm destructive operations
    if [ "$DELETE_DATA" = true ] && [ "$db_state" = "exists_with_data" ]; then
        if ! confirm_action "WARNING: This will DELETE ALL DATA in the $ENVIRONMENT database!"; then
            write_log "INFO" "Deployment cancelled by user"
            exit 0
        fi
    fi
    
    # Step 4: Build module
    if ! build_module; then
        write_log "ERROR" "Module build failed"
        show_summary
        exit 1
    fi
    
    # Step 5: Update build config if needed
    update_build_config "$ENVIRONMENT"
    
    # Step 6: Publish module
    if ! publish_module "$ENVIRONMENT" "$DELETE_DATA"; then
        write_log "ERROR" "Module publish failed"
        show_summary
        exit 1
    fi
    
    # Step 7: Invalidate cache if requested
    if [ "$INVALIDATE_CACHE" = true ]; then
        invalidate_cloudfront "${ENV_CLOUDFRONT[$ENVIRONMENT]}"
    fi
    
    # Step 8: Verify deployment
    if [ "$VERIFY" = true ]; then
        if ! verify_deployment "$ENVIRONMENT"; then
            write_log "WARNING" "Deployment verification failed"
        fi
    fi
    
    # Show summary
    show_summary
    
    # Return exit code
    if [ $ERROR_COUNT -gt 0 ]; then
        exit 1
    fi
    exit 0
}

# Execute deployment
main