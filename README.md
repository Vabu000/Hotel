<!-- # DVO-L3-7F2A91 -->
# Hotel Reservation System

Мікросервісний застосунок на .NET 8: Catalog API, Reservation API, Reviews API,
Aggregator і API Gateway. PostgreSQL використовується каталогом та бронюваннями,
MongoDB — відгуками, Redis — кешем, RabbitMQ — подіями.

## Лабораторна 1: Docker і Compose

Потрібні Docker Desktop із запущеним Linux Engine та Docker Compose v2.
Параметри в `.env.example` — лише навчальні значення для локальної машини; не
використовуйте їх поза нею.

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up --build
```

Веб-точка входу: `http://localhost:8080/`; перевірка здоров'я — `http://localhost:8080/health`.
Gateway маршрутизує HTTP-запити до внутрішніх API. Зупинити сервіси:
`docker compose down`. Для локального тесту: `dotnet test HotelReservation.Tests/HotelReservation.Tests.csproj`.

Compose запускає всі п'ять застосунків та PostgreSQL, MongoDB, Redis і RabbitMQ.
Схема Reservation DB створюється скриптом `docker/postgres/init-databases.sql`;
міграції Catalog DB застосунок виконує під час старту. Персистентні дані Compose
зберігаються у named volumes.

## Лабораторна 2: CI та образи

GitHub Actions workflow `.github/workflows/ci.yml` запускається для Pull Request у
`master`, push у гілки та вручну. Незалежні matrix jobs збирають кожен API, запускають
наявні юніт-тести, а Roslyn analyzers виконуються як блокуюча перевірка. Docker images
спочатку збираються без публікації; окремий job публікує їх у GHCR лише після успіху
перевірок і лише для push. Тег commit SHA унікальний, `latest` створюється тільки для
`master`. Workflow використовує `GITHUB_TOKEN`, а не збережені в репозиторії облікові
дані.

Для захисту `master` у GitHub відкрийте **Settings → Rules → Rulesets** (або
**Branches → Branch protection rules**), забороніть прямий push, вимагайте Pull Request
і позначте `Static analysis`, усі п'ять `Build and test (...)` та всі п'ять
`Build container (...)` як required status checks. Увімкніть вимогу успішного проходження
перевірок перед merge. Налаштування правил гілки потрібно виконати в GitHub; локальний
workflow не має доступу до параметрів репозиторію.

Для демонстрації блокування merge створіть тестову гілку, тимчасово змініть очікуване
значення в `ReservationTests.ReservationStatus_UsesStableDatabaseValues`, відкрийте Pull
Request у `master` і зафіксуйте невдалий `Build and test` та заблоковану кнопку merge.
Поверніть коректне значення окремою зміною й дочекайтеся успішних перевірок.

## Лабораторна 3: Kubernetes

Потрібні Docker Desktop, `kubectl` і встановлений Minikube. Для всіх маніфестів
використано namespace `hotel-reservation-system`; імена образів для навчального кластера
завантажуються локально, тому GHCR не є обов'язковим. Кластер потребує орієнтовно
4 CPU та 6 GiB пам'яті.

```powershell
minikube start --driver=docker --cpus=4 --memory=6g
minikube addons enable metrics-server
minikube addons enable ingress
kubectl get nodes
kubectl get pods -n kube-system

$env:IMAGE_TAG = "1.0.0"
docker compose build
$env:IMAGE_TAG = "1.1.0"
docker compose build

@("catalog-api", "reservation-api", "reviews-api", "aggregator", "api-gateway") | ForEach-Object {
    minikube image load "hotel-reservation-system/${_}:1.0.0"
    minikube image load "hotel-reservation-system/${_}:1.1.0"
}

kubectl apply -f k8s/
kubectl wait --for=condition=available deployment --all -n hotel-reservation-system --timeout=300s
kubectl get pods,deployments,replicasets,services -n hotel-reservation-system -o wide
```

У `k8s/` окремий YAML-файл на кожен Kubernetes object. Namespace ідеться першим за
нумерацією, тому весь каталог застосовується однією командою. Deployment-и сервісів
мають по дві репліки, узгоджені selectors, CPU/memory requests і limits, а також
startup/readiness/liveness probes. Для PostgreSQL, MongoDB і RabbitMQ визначені PVC.
Gateway відкритий через NodePort `30080`:

```powershell
$url = "http://$(minikube ip):30080/"
Invoke-RestMethod $url
minikube service hotel-api-gateway -n hotel-reservation-system --url
```

ConfigMap `hotel-app-config` містить середовище застосунку, адреси gRPC-сервісів,
Redis, назву MongoDB та маршрути Gateway. HTTP API слухають порт `8080`, а сервіси
Catalog, Reservation і Reviews приймають внутрішні gRPC-з'єднання по h2c на `8081`.
Secret `hotel-local-secrets` містить локальні
навчальні значення `POSTGRES_PASSWORD`, `MONGO_ROOT_PASSWORD` і `RABBITMQ_PASSWORD`;
замініть їх перед будь-яким віддаленим розгортанням. Перевірити ConfigMap і передавання
секрету в контейнер без друку значення:

```powershell
kubectl exec deployment/hotel-aggregator -n hotel-reservation-system -- printenv GrpcServices__CatalogService
kubectl exec deployment/hotel-catalog-api -n hotel-reservation-system -- sh -c 'test -n "$POSTGRES_PASSWORD" && echo POSTGRES_PASSWORD=present'
kubectl exec deployment/hotel-reviews-api -n hotel-reservation-system -- sh -c 'test -n "$MONGO_ROOT_PASSWORD" && echo MONGO_ROOT_PASSWORD=present'
```

Керування життєвим циклом і діагностика:

```powershell
# Самовідновлення: видаліть один pod і дочекайтеся доступних реплік.
kubectl delete pod <gateway-pod> -n hotel-reservation-system
kubectl get pods -l app=hotel-api-gateway -n hotel-reservation-system -o wide

# Масштабування, адреси endpoints і розподіл запитів між репліками.
kubectl scale deployment/hotel-api-gateway --replicas=3 -n hotel-reservation-system
kubectl get endpointslices -l kubernetes.io/service-name=hotel-api-gateway -n hotel-reservation-system -o wide
1..15 | ForEach-Object { (Invoke-RestMethod $url).instance }

# Оновлення, історія ревізій та відкат.
kubectl set image deployment/hotel-api-gateway api-gateway=hotel-reservation-system/api-gateway:1.1.0 -n hotel-reservation-system
kubectl rollout status deployment/hotel-api-gateway -n hotel-reservation-system
kubectl rollout history deployment/hotel-api-gateway -n hotel-reservation-system
kubectl rollout undo deployment/hotel-api-gateway -n hotel-reservation-system

# Відтворення ImagePullBackOff, перегляд Events і повернення справного образу.
kubectl set image deployment/hotel-catalog-api catalog-api=hotel-reservation-system/catalog-api:missing-lab-image -n hotel-reservation-system
kubectl get pods -l app=hotel-catalog-api -n hotel-reservation-system
kubectl describe pods -l app=hotel-catalog-api -n hotel-reservation-system
kubectl get events -n hotel-reservation-system --sort-by=.metadata.creationTimestamp
kubectl set image deployment/hotel-catalog-api catalog-api=hotel-reservation-system/catalog-api:1.0.0 -n hotel-reservation-system
kubectl rollout status deployment/hotel-catalog-api -n hotel-reservation-system
kubectl logs deployment/hotel-catalog-api -n hotel-reservation-system
kubectl exec -it deployment/hotel-api-gateway -n hotel-reservation-system -- sh
```

Щоб виконати демонстрацію автоматично й записати вивід команд для звіту
`reports/lab3-demo.txt`, після встановлення інструментів запустіть:

```powershell
.\scripts\lab3-demo.ps1
```

Скрипт створює Minikube кластер із потрібними addon-ами, локально будує й завантажує
обидві версії образів, застосовує маніфести, перевіряє ConfigMap/Secret, самовідновлення,
масштабування, rolling update, відкат і навмисно зламаний тег. Секрети скрипт не друкує.

### Обмеження label Kubernetes

У вимогах вказано `mascot: кабачок`, але значення Kubernetes label може містити лише
ASCII-символи з обмеженого набору. Кириличне значення зробило б усі об'єкти невалідними.
Тому в labels використано валідне `mascot: kabachok`, а оригінал `кабачок` збережено в
annotation `devops-lab/mascot-original`; обов'язкова annotation
`devops-lab/mascot-approved: "true"` додана до об'єктів і шаблонів Pod.

## Локальна перевірка

Перед запуском лабораторних перевірте середовище:

```powershell
dotnet --info
docker info
kubectl get nodes
```

Для повної перевірки потрібні .NET 8 SDK, запущений Docker Engine та Minikube. CI
виконає build/test/analyzer/container build після push; публікація GHCR і налаштування
захисту гілки також потребують доступу до GitHub.
