#!/usr/bin/env bash
# Stellt sicher, dass der CloudFormation-Stack alles für Frontend+Backend enthält.
# - Fehlende VPC/EC2/Endpoints werden neu angelegt (außerhalb des Stacks gelöscht).
# - Ein vorhandener Aurora-DSQL-Cluster wird niemals gelöscht und nicht ersetzt.
# - Fehlt der Cluster in AWS, wird er neu erstellt (oder ein vorhandener mit Name-Tag importiert).
set -euo pipefail

STACK_NAME="${STACK_NAME:?}"
PROJECT_NAME="${PROJECT_NAME:-taetigkeitsbericht}"
TEMPLATE_FILE="${TEMPLATE_FILE:-infra/cloudformation/taetigkeitsbericht-aws.yml}"
TEMPLATE_FILE="$(realpath "$TEMPLATE_FILE")"
FORCE_REFRESH="${FORCE_REFRESH:-false}"
KEY_NAME="${KEY_NAME:-}"
DSQL_NAME_TAG="${DSQL_NAME_TAG:-taetigkeitsbericht-fullstack-dsql}"
BUCKET_NAME="${PROJECT_NAME}-artifacts-$(aws sts get-caller-identity --query Account --output text)-${AWS_REGION:?}"

log() { echo "=== $*"; }

stack_status() {
  aws cloudformation describe-stacks --stack-name "$STACK_NAME" \
    --query "Stacks[0].StackStatus" --output text 2>/dev/null || true
}

wait_stack_idle() {
  local n=0 st
  while true; do
    st="$(stack_status)"
    case "$st" in
      ""|"None")
        return 0
        ;;
      *IN_PROGRESS*)
        n=$((n + 1))
        if [ "$n" -gt 180 ]; then
          echo "::error::Timeout: Stack bleibt in $st"
          exit 1
        fi
        echo "Warte auf Stack-Ende ($st) … $n"
        sleep 20
        ;;
      *)
        return 0
        ;;
    esac
  done
}

dump_stack_events() {
  aws cloudformation describe-stack-events --stack-name "$STACK_NAME" \
    --query "StackEvents[:25].[LogicalResourceId,ResourceStatus,ResourceStatusReason]" \
    --output text 2>/dev/null || true
}

phys_id() {
  aws cloudformation describe-stack-resource \
    --stack-name "$STACK_NAME" \
    --logical-resource-id "$1" \
    --query "StackResourceDetail.PhysicalResourceId" --output text 2>/dev/null || true
}

dsql_id_from_phys() {
  local p="${1:-}"
  if [ -z "$p" ] || [ "$p" = "None" ]; then
    return 1
  fi
  if [[ "$p" == arn:aws:dsql:* ]]; then
    echo "${p##*/}"
  else
    echo "$p"
  fi
}

dsql_is_alive() {
  local id="${1:-}"
  [ -n "$id" ] && [ "$id" != "None" ] || return 1
  local st
  st="$(aws dsql get-cluster --identifier "$id" --query status --output text 2>/dev/null || true)"
  case "$st" in
    ACTIVE|IDLE|CREATING|UPDATING) return 0 ;;
    *) return 1 ;;
  esac
}

instance_is_alive() {
  local id="${1:-}"
  [ -n "$id" ] && [ "$id" != "None" ] || return 1
  local st
  st="$(aws ec2 describe-instances --instance-ids "$id" \
    --query "Reservations[0].Instances[0].State.Name" --output text 2>/dev/null || echo missing)"
  case "$st" in
    running|pending|stopping|stopped) return 0 ;;
    *) return 1 ;;
  esac
}

vpc_is_alive() {
  local id="${1:-}"
  [ -n "$id" ] && [ "$id" != "None" ] || return 1
  aws ec2 describe-vpcs --vpc-ids "$id" --query "Vpcs[0].VpcId" --output text >/dev/null 2>&1
}

endpoint_is_alive() {
  local id="${1:-}"
  [ -n "$id" ] && [ "$id" != "None" ] || return 1
  local st
  st="$(aws ec2 describe-vpc-endpoints --vpc-endpoint-ids "$id" \
    --query "VpcEndpoints[0].State" --output text 2>/dev/null || echo missing)"
  case "$st" in
    available|pending) return 0 ;;
    *) return 1 ;;
  esac
}

find_tagged_dsql() {
  local id arn name extras=0 first=""
  local ids
  ids="$(aws dsql list-clusters --query "clusters[].identifier" --output text 2>/dev/null || true)"
  for id in $ids; do
    [ -n "$id" ] && [ "$id" != "None" ] || continue
    dsql_is_alive "$id" || continue
    arn="$(aws dsql get-cluster --identifier "$id" --query arn --output text)"
    name="$(aws dsql list-tags-for-resource --resource-arn "$arn" --query "tags.Name" --output text 2>/dev/null || true)"
    if [ "$name" = "$DSQL_NAME_TAG" ]; then
      if [ -z "$first" ]; then
        first="$id"
      else
        extras=$((extras + 1))
      fi
    fi
  done
  if [ -n "$first" ]; then
    if [ "$extras" -gt 0 ]; then
      echo "::warning::Mehrere DSQL-Cluster mit Tag Name=${DSQL_NAME_TAG}. Nutze ${first}, lösche keine anderen." >&2
    fi
    echo "$first"
    return 0
  fi
  return 1
}

cfn_deploy() {
  local compute="$1" dsql="$2" refresh="$3" endpoint="${4:-true}"
  local params=(
    "ProjectName=${PROJECT_NAME}"
    "ForceInstanceRefresh=${refresh}"
    "ProvisionCompute=${compute}"
    "ProvisionDsql=${dsql}"
    "ProvisionDsqlEndpoint=${endpoint}"
  )
  if [ -n "$KEY_NAME" ]; then
    params+=("KeyName=${KEY_NAME}")
  fi
  log "CloudFormation deploy ProvisionCompute=${compute} ProvisionDsql=${dsql} ProvisionDsqlEndpoint=${endpoint} ForceInstanceRefresh=${refresh}"
  if ! aws cloudformation deploy \
    --stack-name "$STACK_NAME" \
    --template-file "$TEMPLATE_FILE" \
    --capabilities CAPABILITY_NAMED_IAM \
    --no-fail-on-empty-changeset \
    --parameter-overrides "${params[@]}"; then
    echo "::error::CloudFormation deploy fehlgeschlagen"
    dump_stack_events
    return 1
  fi
}

import_dsql() {
  local id="$1"
  local cs import_json
  cs="import-dsql-$(date +%s)"
  import_json="$(mktemp)"
  cat > "$import_json" <<EOF
[
  {
    "ResourceType": "AWS::DSQL::Cluster",
    "LogicalResourceId": "DsqlCluster",
    "ResourceIdentifier": {
      "Identifier": "${id}"
    }
  }
]
EOF
  log "Importiere vorhandenen DSQL-Cluster ${id} in den Stack (kein neues Cluster)"
  local params=(
    "ParameterKey=ProjectName,ParameterValue=${PROJECT_NAME}"
    "ParameterKey=ForceInstanceRefresh,ParameterValue=false"
    "ParameterKey=ProvisionCompute,ParameterValue=false"
    "ParameterKey=ProvisionDsql,ParameterValue=true"
    "ParameterKey=ProvisionDsqlEndpoint,ParameterValue=false"
  )
  if [ -n "$KEY_NAME" ]; then
    params+=("ParameterKey=KeyName,ParameterValue=${KEY_NAME}")
  fi
  if ! aws cloudformation create-change-set \
    --stack-name "$STACK_NAME" \
    --change-set-name "$cs" \
    --change-set-type IMPORT \
    --resources-to-import "file://${import_json}" \
    --template-body "file://${TEMPLATE_FILE}" \
    --capabilities CAPABILITY_NAMED_IAM \
    --parameters "${params[@]}"; then
    echo "::error::DSQL-Import-Changeset konnte nicht erzeugt werden"
    cat "$import_json"
    return 1
  fi
  if ! aws cloudformation wait change-set-create-complete \
    --stack-name "$STACK_NAME" --change-set-name "$cs"; then
    aws cloudformation describe-change-set --stack-name "$STACK_NAME" --change-set-name "$cs" --output json || true
    return 1
  fi
  aws cloudformation execute-change-set --stack-name "$STACK_NAME" --change-set-name "$cs"
  aws cloudformation wait stack-import-complete --stack-name "$STACK_NAME"
  log "DSQL-Import abgeschlossen"
}

delete_orphaned_iam_and_codedeploy() {
  local role="${PROJECT_NAME}-ec2-role"
  local profile
  log "Kein Stack: verwaiste IAM/CodeDeploy-Namen räumen (DSQL und S3 bleiben)"
  profile="$(aws iam list-instance-profiles --query "InstanceProfiles[?Roles[?RoleName=='${role}']].InstanceProfileName" --output text 2>/dev/null || true)"
  if [ -n "$profile" ] && [ "$profile" != "None" ]; then
    aws iam remove-role-from-instance-profile --instance-profile-name "$profile" --role-name "$role" 2>/dev/null || true
    aws iam delete-instance-profile --instance-profile-name "$profile" 2>/dev/null || true
  fi
  aws iam delete-role-policy --role-name "$role" --policy-name DsqlConnect 2>/dev/null || true
  aws iam delete-role-policy --role-name "$role" --policy-name ArtifactAndJwt 2>/dev/null || true
  aws iam detach-role-policy --role-name "$role" --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore 2>/dev/null || true
  aws iam detach-role-policy --role-name "$role" --policy-arn arn:aws:iam::aws:policy/service-role/AmazonEC2RoleforAWSCodeDeploy 2>/dev/null || true
  aws iam delete-role --role-name "$role" 2>/dev/null || true

  for app in "${PROJECT_NAME}-backend" "${PROJECT_NAME}-frontend"; do
    aws deploy delete-deployment-group --application-name "$app" --deployment-group-name backend 2>/dev/null || true
    aws deploy delete-deployment-group --application-name "$app" --deployment-group-name frontend 2>/dev/null || true
    aws deploy delete-application --application-name "$app" 2>/dev/null || true
  done
}

import_artifact_bucket_then_core() {
  local cs import_json
  cs="import-bucket-$(date +%s)"
  import_json="$(mktemp)"
  cat > "$import_json" <<EOF
[
  {
    "ResourceType": "AWS::S3::Bucket",
    "LogicalResourceId": "ArtifactBucket",
    "ResourceIdentifier": {
      "BucketName": "${BUCKET_NAME}"
    }
  }
]
EOF
  log "Importiere vorhandenen Artifact-Bucket ${BUCKET_NAME}"
  local params=(
    "ParameterKey=ProjectName,ParameterValue=${PROJECT_NAME}"
    "ParameterKey=ForceInstanceRefresh,ParameterValue=false"
    "ParameterKey=ProvisionCompute,ParameterValue=false"
    "ParameterKey=ProvisionDsql,ParameterValue=false"
    "ParameterKey=ProvisionDsqlEndpoint,ParameterValue=false"
  )
  if [ -n "$KEY_NAME" ]; then
    params+=("ParameterKey=KeyName,ParameterValue=${KEY_NAME}")
  fi
  aws cloudformation create-change-set \
    --stack-name "$STACK_NAME" \
    --change-set-name "$cs" \
    --change-set-type IMPORT \
    --resources-to-import "file://${import_json}" \
    --template-body "file://${TEMPLATE_FILE}" \
    --capabilities CAPABILITY_NAMED_IAM \
    --parameters "${params[@]}"
  aws cloudformation wait change-set-create-complete \
    --stack-name "$STACK_NAME" --change-set-name "$cs"
  aws cloudformation execute-change-set --stack-name "$STACK_NAME" --change-set-name "$cs"
  aws cloudformation wait stack-import-complete --stack-name "$STACK_NAME"
}

fix_failed_stack() {
  local st
  st="$(stack_status)"
  case "$st" in
    UPDATE_ROLLBACK_FAILED|UPDATE_FAILED)
      log "continue-update-rollback ($st)"
      aws cloudformation continue-update-rollback --stack-name "$STACK_NAME" || true
      wait_stack_idle
      ;;
    ROLLBACK_COMPLETE|CREATE_FAILED)
      log "Stack $st – löschen (DSQL DeletionPolicy=Retain, Cluster bleibt) und neu aufbauen"
      aws cloudformation delete-stack --stack-name "$STACK_NAME"
      aws cloudformation wait stack-delete-complete --stack-name "$STACK_NAME"
      ;;
    DELETE_FAILED)
      log "DELETE_FAILED – erneuter delete-stack"
      aws cloudformation delete-stack --stack-name "$STACK_NAME" || true
      wait_stack_idle
      ;;
  esac
}

verify_ready() {
  local dsql_phys dsql_id be fe vpc ep
  dsql_phys="$(phys_id DsqlCluster)"
  dsql_id="$(dsql_id_from_phys "$dsql_phys" || true)"
  be="$(phys_id BackendInstance)"
  fe="$(phys_id FrontendInstance)"
  vpc="$(phys_id Vpc)"
  ep="$(phys_id DsqlVpcEndpoint)"
  log "Prüfe Zielzustand: DSQL=$dsql_id Backend=$be Frontend=$fe VPC=$vpc Endpoint=$ep"
  if ! dsql_is_alive "$dsql_id"; then
    echo "::error::Aurora DSQL fehlt oder ist nicht aktiv. Frontend/Backend können so nicht arbeiten."
    exit 1
  fi
  if ! vpc_is_alive "$vpc"; then
    echo "::error::VPC fehlt nach dem Deploy."
    exit 1
  fi
  if ! instance_is_alive "$be" || ! instance_is_alive "$fe"; then
    echo "::error::EC2 Backend/Frontend fehlen oder sind beendet."
    exit 1
  fi
  if ! endpoint_is_alive "$ep"; then
    echo "::error::DSQL VPC-Endpoint (PrivateLink) fehlt."
    exit 1
  fi
  log "Stack bereit: VPC, EC2, DSQL und PrivateLink sind vorhanden. DSQL wurde nicht gelöscht."
}

# --- Ablauf ---
log "Region=${AWS_REGION} Stack=${STACK_NAME}"
wait_stack_idle
fix_failed_stack
wait_stack_idle

ST="$(stack_status)"
if [ -z "$ST" ] || [ "$ST" = "None" ]; then
  log "Kein Stack – vollständige Anlage"
  tagged="$(find_tagged_dsql || true)"
  bucket_exists=0
  if aws s3api head-bucket --bucket "$BUCKET_NAME" 2>/dev/null; then
    bucket_exists=1
  fi
  delete_orphaned_iam_and_codedeploy
  if [ "$bucket_exists" = "1" ]; then
    import_artifact_bucket_then_core
  fi
  if [ -n "$tagged" ]; then
    log "Vorhandener DSQL-Cluster ${tagged} – importieren, nicht neu anlegen"
    if [ -z "$(stack_status)" ] || [ "$(stack_status)" = "None" ]; then
      cfn_deploy false false false
    fi
    import_dsql "$tagged"
    cfn_deploy true true false
  else
    cfn_deploy true true false
  fi
  verify_ready
  exit 0
fi

log "Stack existiert ($ST) – Drift prüfen"

dsql_phys="$(phys_id DsqlCluster)"
dsql_id="$(dsql_id_from_phys "$dsql_phys" || true)"
tagged="$(find_tagged_dsql || true)"
be="$(phys_id BackendInstance)"
fe="$(phys_id FrontendInstance)"
vpc="$(phys_id Vpc)"
ep="$(phys_id DsqlVpcEndpoint)"

dsql_ok=0
if dsql_is_alive "$dsql_id"; then
  dsql_ok=1
  log "DSQL im Stack ist aktiv (${dsql_id}) – bleibt erhalten"
elif [ -n "$tagged" ]; then
  log "DSQL-Stack-ID tot/fehlend, aber Cluster ${tagged} existiert – wird importiert, nicht gelöscht"
else
  log "Kein aktiver DSQL-Cluster – wird neu erstellt"
fi

vpc_ok=0
instance_ok=0
endpoint_ok=0
vpc_is_alive "$vpc" && vpc_ok=1
if instance_is_alive "$be" && instance_is_alive "$fe"; then
  instance_ok=1
fi
endpoint_is_alive "$ep" && endpoint_ok=1

log "Ist-Zustand: vpc_ok=${vpc_ok} instance_ok=${instance_ok} endpoint_ok=${endpoint_ok} dsql_ok=${dsql_ok}"

need_compute_reset=0
need_instance_refresh=0
need_endpoint_reset=0
need_dsql_unregister=0
need_dsql_import=0

if [ "$dsql_ok" != "1" ]; then
  if [ -n "$dsql_phys" ] && [ "$dsql_phys" != "None" ]; then
    need_dsql_unregister=1
  fi
  if [ -n "$tagged" ]; then
    need_dsql_import=1
  fi
fi

if [ "$vpc_ok" != "1" ]; then
  need_compute_reset=1
elif [ "$endpoint_ok" != "1" ]; then
  need_endpoint_reset=1
elif [ "$instance_ok" != "1" ]; then
  need_instance_refresh=1
fi

if [ "$FORCE_REFRESH" = "true" ]; then
  need_instance_refresh=1
fi

if [ "$need_dsql_unregister" = "1" ] || [ "$need_compute_reset" = "1" ]; then
  keep_dsql=true
  if [ "$dsql_ok" != "1" ]; then
    keep_dsql=false
  fi
  log "Zwischenstand: Compute/Netz aus dem Stack nehmen (fehlende Ressourcen). DSQL im Stack=${keep_dsql}"
  cfn_deploy false "$keep_dsql" false
fi

if [ "$need_dsql_import" = "1" ]; then
  current="$(phys_id DsqlCluster)"
  if ! dsql_is_alive "$(dsql_id_from_phys "$current" || true)"; then
    import_dsql "$tagged"
  fi
fi

if [ "$need_endpoint_reset" = "1" ] && [ "$need_compute_reset" != "1" ]; then
  log "PrivateLink-Endpoint fehlt – aus dem Stack nehmen (Retain) und neu anlegen. DSQL bleibt."
  cfn_deploy true true false false
fi

refresh=false
if [ "$need_instance_refresh" = "1" ] && [ "$need_compute_reset" != "1" ]; then
  refresh=true
fi
if [ "$FORCE_REFRESH" = "true" ]; then
  refresh=true
fi

if ! cfn_deploy true true "$refresh"; then
  log "Voll-Deploy fehlgeschlagen – Stack-Status prüfen und Compute ggf. neu aufbauen (DSQL bleibt)"
  wait_stack_idle
  fix_failed_stack
  wait_stack_idle
  cfn_deploy false true false
  cfn_deploy true true false
fi

if [ "$need_instance_refresh" = "1" ] && [ "$need_compute_reset" != "1" ]; then
  if ! instance_is_alive "$(phys_id BackendInstance)" || ! instance_is_alive "$(phys_id FrontendInstance)"; then
    log "Instance-Refresh hat nicht gereicht – Compute neu anlegen, DSQL bleibt"
    cfn_deploy false true false
    cfn_deploy true true false
  fi
fi

verify_ready
