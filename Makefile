.PHONY: help deploy-local deploy-remote build-agent build-homepage sync-remote verify-local verify-remote

# Default target
help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# =============================================================================
# BUILD TARGETS
# =============================================================================

build-agent: ## Build terraria-agent Docker image
	@echo "🔨 Building terraria-agent..."
	docker build -t terraria-agent:latest ./terraria-agent/
	@echo "✅ terraria-agent built"

build-homepage: ## Build homepage Docker image
	@echo "🔨 Building homepage..."
	docker build -t homepage:latest ./homepage/
	@echo "✅ homepage built"

build-all: build-agent build-homepage ## Build all Docker images

# =============================================================================
# LOCAL DEPLOYMENT
# =============================================================================

import-local: build-all ## Import images to local k3s
	@echo "📥 Importing images to local k3s..."
	docker save terraria-agent:latest | sudo k3s ctr images import -
	docker save homepage:latest | sudo k3s ctr images import -
	@echo "✅ Images imported to local k3s"

deploy-local: import-local ## Deploy all services to local cluster
	@echo "🚀 Deploying to local cluster..."
	# Apply base manifests
	sudo k3s kubectl apply -f terraria-server/namespace.yaml
	sudo k3s kubectl apply -f terraria-server/pvc.yaml
	sudo k3s kubectl apply -f terraria-server/configmap.yaml
	sudo k3s kubectl apply -f terraria-server/statefulset.yaml
	sudo k3s kubectl apply -f terraria-server/service.yaml
	# Apply nginx ingress
	sudo k3s kubectl apply -f terraria-server/local-ingress.yaml
	# Apply agent
	sudo k3s kubectl apply -f terraria-agent/k8s/
	# Apply homepage
	sudo k3s kubectl apply -f homepage/k8s/
	# Apply other services
	sudo k3s kubectl apply -f investigation-team-api/k8s/
	sudo k3s kubectl apply -f investigation-team-frontend/k8s/
	sudo k3s kubectl apply -f investigation-team-chat-backend/k8s/
	sudo k3s kubectl apply -f supermarket-api/k8s/
	sudo k3s kubectl apply -f supermarket-frontend/k8s/
	@echo "✅ Local deployment complete"

restart-local: ## Restart all deployments in local cluster
	@echo "🔄 Restarting local services..."
	sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria
	sudo k3s kubectl rollout restart deployment/homepage -n homepage
	sudo k3s kubectl rollout restart deployment/investigation-team-frontend -n investigation-team-frontend
	sudo k3s kubectl rollout restart deployment/investigation-team-chat-api -n investigation-team-frontend
	sudo k3s kubectl rollout restart deployment/supermarket-frontend -n supermarket
	sudo k3s kubectl rollout restart deployment/supermarket-api -n supermarket
	sudo k3s kubectl rollout restart deployment/investigation-team-api -n investigation-team
	@echo "✅ Local services restarted"

# =============================================================================
# REMOTE DEPLOYMENT
# =============================================================================

import-remote: build-all ## Import images to remote k3s
	@echo "📥 Importing images to remote k3s..."
	docker save terraria-agent:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	docker save homepage:latest | ssh roman@srv01.gaming.andalusiaone.com "sudo k3s ctr images import -"
	@echo "✅ Images imported to remote k3s"

deploy-remote: import-remote ## Deploy all services to remote cluster
	@echo "🚀 Deploying to remote cluster..."
	# Apply base manifests
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/namespace.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/pvc.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/configmap.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/statefulset.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/service.yaml
	# Apply nginx ingress
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-server/remote-ingress.yaml
	# Apply agent
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-agent/k8s/deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-agent/k8s/service.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < terraria-agent/k8s/secret.yaml
	# Apply homepage
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < homepage/k8s/deployment.yaml
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < homepage/k8s/service.yaml
	# Apply other services
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < investigation-team-api/k8s/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < investigation-team-frontend/k8s/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < investigation-team-chat-backend/k8s/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < supermarket-api/k8s/
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl apply -f /dev/stdin" < supermarket-frontend/k8s/
	@echo "✅ Remote deployment complete"

restart-remote: ## Restart all deployments in remote cluster
	@echo "🔄 Restarting remote services..."
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/terraria-agent -n terraria"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/homepage -n homepage"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-frontend -n investigation-team-frontend"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-chat-api -n investigation-team-frontend"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/supermarket-frontend -n supermarket"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/supermarket-api -n supermarket"
	ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl rollout restart deployment/investigation-team-api -n investigation-team"
	@echo "✅ Remote services restarted"

# =============================================================================
# SYNC (Build + Deploy to Remote)
# =============================================================================

sync-remote: import-remote restart-remote ## Sync latest code to remote and restart
	@echo "✅ Remote synced with latest code"

# =============================================================================
# VERIFICATION
# =============================================================================

verify-local: ## Verify all services are running locally
	@echo "🔍 Verifying local cluster..."
	@echo ""
	@echo "Pods:"
	@sudo k3s kubectl get pods --all-namespaces | grep -v kube-system | grep -v coredns | grep -v local-path | grep -v metrics-server | grep -v svclb | grep -v traefik
	@echo ""
	@echo "Ingress:"
	@sudo k3s kubectl get ingress --all-namespaces
	@echo ""
	@echo "Testing services:"
	@curl -s -m 5 http://172.30.138.92:31931/ | head -1 && echo " - Homepage OK" || echo " - Homepage FAILED"
	@curl -s -m 5 http://172.30.138.92:31931/terraria-agent/swagger/index.html | head -1 && echo " - Agent OK" || echo " - Agent FAILED"
	@curl -s -m 5 http://172.30.138.92:31931/it/ | head -1 && echo " - InvestigationTeam OK" || echo " - InvestigationTeam FAILED"
	@curl -s -m 5 http://172.30.138.92:31931/supermarket/ | head -1 && echo " - Supermarket OK" || echo " - Supermarket FAILED"

verify-remote: ## Verify all services are running remotely
	@echo "🔍 Verifying remote cluster..."
	@echo ""
	@echo "Pods:"
	@ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get pods --all-namespaces | grep -v kube-system"
	@echo ""
	@echo "Ingress:"
	@ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get ingress --all-namespaces"
	@echo ""
	@echo "Testing services:"
	@curl -s -m 5 http://gaming.andalusiaone.com:30808/ | head -1 && echo " - Homepage OK" || echo " - Homepage FAILED"
	@curl -s -m 5 http://gaming.andalusiaone.com:30808/terraria-agent/swagger/index.html | head -1 && echo " - Agent OK" || echo " - Agent FAILED"
	@curl -s -m 5 http://gaming.andalusiaone.com:30808/it/ | head -1 && echo " - InvestigationTeam OK" || echo " - InvestigationTeam FAILED"
	@curl -s -m 5 http://gaming.andalusiaone.com:30808/supermarket/ | head -1 && echo " - Supermarket OK" || echo " - Supermarket FAILED"

verify-all: verify-local verify-remote ## Verify both clusters

# =============================================================================
# QUICK COMMANDS
# =============================================================================

deploy: deploy-local sync-remote ## Deploy to both clusters
	@echo "✅ Deployed to both clusters"

status: ## Show status of both clusters
	@echo "=== LOCAL CLUSTER ==="
	@sudo k3s kubectl get pods --all-namespaces | grep -v kube-system | grep -v coredns | grep -v local-path | grep -v metrics-server | grep -v svclb | grep -v traefik
	@echo ""
	@echo "=== REMOTE CLUSTER ==="
	@ssh roman@srv01.gaming.andalusiaone.com "sudo k3s kubectl get pods --all-namespaces | grep -v kube-system"
