# 低空经济与无人机交通管理系统（UTM）- API 接口规范文档

## 1. 概述

### 1.1 文档目的
本文档定义了低空经济与无人机交通管理系统（UTM）的外部 API 接口规范，包括接口定义、请求/响应格式、错误码等信息。

### 1.2 基础信息
- **Base URL**：`http://localhost:5000/api/v1`
- **协议**：HTTP/HTTPS
- **数据格式**：JSON
- **编码**：UTF-8
- **API 文档**：`/swagger`（Swagger UI）

### 1.3 通用响应格式

#### 成功响应
```json
{
  "code": 200,
  "message": "success",
  "data": {}
}
```

#### 错误响应
```json
{
  "code": 400,
  "message": "错误描述",
  "details": "详细错误信息"
}
```

### 1.4 HTTP 状态码

| 状态码 | 说明 |
|-------|------|
| 200 OK | 请求成功 |
| 201 Created | 资源创建成功 |
| 204 No Content | 操作成功，无返回内容 |
| 400 Bad Request | 请求参数错误 |
| 401 Unauthorized | 未授权 |
| 403 Forbidden | 禁止访问 |
| 404 Not Found | 资源不存在 |
| 409 Conflict | 资源冲突 |
| 429 Too Many Requests | 请求频率超限 |
| 500 Internal Server Error | 服务器内部错误 |
| 503 Service Unavailable | 服务不可用 |

### 1.5 业务错误码

| 错误码 | 说明 |
|-------|------|
| 10001 | 无人机不存在 |
| 10002 | 无人机已存在 |
| 10003 | 无人机状态无效 |
| 20001 | 遥测数据格式错误 |
| 20002 | 遥测数据超出范围 |
| 30001 | 冲突检测服务不可用 |
| 30002 | 避让规划失败 |

---

## 2. 数据模型

### 2.1 无人机 (Drone)

```json
{
  "id": "DRONE-001",
  "name": "Delivery Drone 01",
  "type": "Multirotor",
  "status": "Flying",
  "currentPosition": {
    "longitude": 116.3972,
    "latitude": 39.9075,
    "altitude": 100.0
  },
  "velocity": {
    "east": 10.5,
    "north": 5.2,
    "up": 0.0
  },
  "batteryLevel": 85.5,
  "safetyRadius": 2.0,
  "priority": 5,
  "lastUpdateTime": "2024-01-15T10:30:00Z"
}
```

#### 字段说明

| 字段 | 类型 | 说明 |
|-----|------|------|
| id | string | 无人机唯一标识 |
| name | string | 无人机名称 |
| type | enum | 无人机类型：Multirotor / FixedWing / Hybrid |
| status | enum | 状态：Idle / Flying / Hovering / Landing / Landed / Emergency |
| currentPosition | object | 当前三维位置 |
| velocity | object | 三维速度向量（米/秒） |
| batteryLevel | double | 电量百分比（0-100） |
| safetyRadius | double | 安全半径（米） |
| priority | int | 优先级（1-10，10最高） |
| lastUpdateTime | datetime | 最后更新时间 |

### 2.2 三维位置 (Position3D)

```json
{
  "longitude": 116.3972,
  "latitude": 39.9075,
  "altitude": 100.0
}
```

#### 字段说明

| 字段 | 类型 | 说明 | 单位 |
|-----|------|------|------|
| longitude | double | 经度 | 度（WGS84） |
| latitude | double | 纬度 | 度（WGS84） |
| altitude | double | 海拔高度 | 米 |

### 2.3 速度向量 (Velocity3D)

```json
{
  "east": 10.5,
  "north": 5.2,
  "up": 0.0
}
```

#### 字段说明

| 字段 | 类型 | 说明 | 单位 |
|-----|------|------|------|
| east | double | 东向速度分量 | 米/秒 |
| north | double | 北向速度分量 | 米/秒 |
| up | double | 垂向速度分量 | 米/秒 |

### 2.4 遥测数据 (TelemetryData)

```json
{
  "droneId": "DRONE-001",
  "timestamp": "2024-01-15T10:30:00.123Z",
  "sequenceNumber": 12345,
  "position": {
    "longitude": 116.3972,
    "latitude": 39.9075,
    "altitude": 100.0
  },
  "velocity": {
    "east": 10.5,
    "north": 5.2,
    "up": 0.0
  },
  "batteryLevel": 85.5,
  "signalStrength": 95.0,
  "status": "Flying"
}
```

### 2.5 冲突告警 (ConflictAlert)

```json
{
  "id": "conflict-001",
  "droneIds": ["DRONE-001", "DRONE-002"],
  "severity": "High",
  "tcpa": 15.5,
  "dcpa": 25.0,
  "predictedPosition": {
    "longitude": 116.3975,
    "latitude": 39.9078,
    "altitude": 100.0
  },
  "detectedTime": "2024-01-15T10:30:00Z",
  "avoidanceAdvice": {
    "yieldingDroneId": "DRONE-001",
    "keepingDroneId": "DRONE-002",
    "recommendedAction": "Climb",
    "recommendedAltitudeChange": 20.0
  }
}
```

#### 严重程度枚举

| 值 | 说明 |
|----|------|
| Low | 低风险 |
| Medium | 中风险 |
| High | 高风险 |
| Critical | 危急 |

### 2.6 轨迹点 (TrajectoryPoint)

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "position": {
    "longitude": 116.3972,
    "latitude": 39.9075,
    "altitude": 100.0
  }
}
```

### 2.7 分页响应

```json
{
  "items": [],
  "totalCount": 100,
  "pageIndex": 0,
  "pageSize": 20,
  "totalPages": 5
}
```

---

## 3. 无人机管理 API

### 3.1 获取所有无人机

**接口描述**：获取系统中所有无人机的列表

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/drones` |
| 权限 | 只读 |

#### 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| status | string | 否 | 按状态筛选 |
| type | string | 否 | 按类型筛选 |
| pageIndex | int | 否 | 页码，默认 0 |
| pageSize | int | 否 | 每页数量，默认 20 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "items": [
      {
        "id": "DRONE-001",
        "name": "Delivery Drone 01",
        "type": "Multirotor",
        "status": "Flying",
        "currentPosition": {
          "longitude": 116.3972,
          "latitude": 39.9075,
          "altitude": 100.0
        },
        "batteryLevel": 85.5,
        "lastUpdateTime": "2024-01-15T10:30:00Z"
      }
    ],
    "totalCount": 1,
    "pageIndex": 0,
    "pageSize": 20,
    "totalPages": 1
  }
}
```

### 3.2 获取单架无人机详情

**接口描述**：根据无人机 ID 获取详细信息

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/drones/{id}` |
| 权限 | 只读 |

#### 路径参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| id | string | 是 | 无人机 ID |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "id": "DRONE-001",
    "name": "Delivery Drone 01",
    "type": "Multirotor",
    "status": "Flying",
    "currentPosition": {
      "longitude": 116.3972,
      "latitude": 39.9075,
      "altitude": 100.0
    },
    "velocity": {
      "east": 10.5,
      "north": 5.2,
      "up": 0.0
    },
    "batteryLevel": 85.5,
    "safetyRadius": 2.0,
    "priority": 5,
    "lastUpdateTime": "2024-01-15T10:30:00Z"
  }
}
```

### 3.3 注册无人机

**接口描述**：向系统注册一架新无人机

| 属性 | 值 |
|-----|----|
| HTTP 方法 | POST |
| 路径 | `/drones` |
| 权限 | 可写 |

#### 请求体

```json
{
  "id": "DRONE-001",
  "name": "Delivery Drone 01",
  "type": "Multirotor",
  "initialPosition": {
    "longitude": 116.3972,
    "latitude": 39.9075,
    "altitude": 0.0
  },
  "safetyRadius": 2.0,
  "priority": 5
}
```

#### 字段说明

| 字段 | 类型 | 必填 | 说明 |
|-----|------|------|------|
| id | string | 是 | 无人机唯一标识 |
| name | string | 否 | 无人机名称 |
| type | string | 是 | 无人机类型 |
| initialPosition | object | 否 | 初始位置 |
| safetyRadius | double | 否 | 安全半径，默认 2.0 米 |
| priority | int | 否 | 优先级，默认 5 |

#### 响应示例

```json
{
  "code": 201,
  "message": "Drone registered successfully",
  "data": {
    "id": "DRONE-001",
    "name": "Delivery Drone 01",
    "type": "Multirotor",
    "status": "Idle",
    "currentPosition": {
      "longitude": 116.3972,
      "latitude": 39.9075,
      "altitude": 0.0
    },
    "safetyRadius": 2.0,
    "priority": 5,
    "lastUpdateTime": "2024-01-15T10:30:00Z"
  }
}
```

### 3.4 更新无人机状态

**接口描述**：更新无人机的状态（如起飞、降落等）

| 属性 | 值 |
|-----|----|
| HTTP 方法 | PUT |
| 路径 | `/drones/{id}/status` |
| 权限 | 可写 |

#### 路径参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| id | string | 是 | 无人机 ID |

#### 请求体

```json
{
  "status": "Flying"
}
```

#### 响应示例

```json
{
  "code": 200,
  "message": "Status updated successfully",
  "data": {
    "id": "DRONE-001",
    "status": "Flying",
    "lastUpdateTime": "2024-01-15T10:30:00Z"
  }
}
```

### 3.5 删除无人机

**接口描述**：从系统中移除无人机

| 属性 | 值 |
|-----|----|
| HTTP 方法 | DELETE |
| 路径 | `/drones/{id}` |
| 权限 | 可写 |

#### 路径参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| id | string | 是 | 无人机 ID |

#### 响应示例

```json
{
  "code": 200,
  "message": "Drone removed successfully"
}
```

### 3.6 获取无人机数量统计

**接口描述**：获取各状态无人机的数量统计

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/drones/stats/count` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "total": 50,
    "flying": 35,
    "hovering": 5,
    "landed": 8,
    "idle": 2
  }
}
```

---

## 4. 遥测数据 API

### 4.1 上报遥测数据

**接口描述**：无人机上报实时遥测数据（HTTP 方式）

| 属性 | 值 |
|-----|----|
| HTTP 方法 | POST |
| 路径 | `/telemetry` |
| 权限 | 可写 |
| 限流 | 1000 次/秒 |

#### 请求体

```json
{
  "droneId": "DRONE-001",
  "timestamp": "2024-01-15T10:30:00.123Z",
  "sequenceNumber": 12345,
  "position": {
    "longitude": 116.3972,
    "latitude": 39.9075,
    "altitude": 100.0
  },
  "velocity": {
    "east": 10.5,
    "north": 5.2,
    "up": 0.0
  },
  "batteryLevel": 85.5,
  "signalStrength": 95.0,
  "status": "Flying"
}
```

#### 字段说明

| 字段 | 类型 | 必填 | 说明 |
|-----|------|------|------|
| droneId | string | 是 | 无人机 ID |
| timestamp | datetime | 是 | 数据采集时间戳 |
| sequenceNumber | long | 否 | 序列号，用于检测丢包 |
| position | object | 是 | 当前位置 |
| velocity | object | 是 | 当前速度 |
| batteryLevel | double | 否 | 电量百分比 |
| signalStrength | double | 否 | 信号强度 |
| status | string | 否 | 当前状态 |

#### 响应示例

```json
{
  "code": 200,
  "message": "Telemetry received",
  "data": {
    "receivedAt": "2024-01-15T10:30:00.125Z",
    "processingLatencyMs": 2.3
  }
}
```

### 4.2 批量上报遥测数据

**接口描述**：批量上报多架无人机的遥测数据

| 属性 | 值 |
|-----|----|
| HTTP 方法 | POST |
| 路径 | `/telemetry/batch` |
| 权限 | 可写 |
| 限流 | 100 次/秒 |

#### 请求体

```json
{
  "telemetryData": [
    {
      "droneId": "DRONE-001",
      "timestamp": "2024-01-15T10:30:00.123Z",
      "position": { "longitude": 116.3972, "latitude": 39.9075, "altitude": 100.0 },
      "velocity": { "east": 10.5, "north": 5.2, "up": 0.0 }
    },
    {
      "droneId": "DRONE-002",
      "timestamp": "2024-01-15T10:30:00.124Z",
      "position": { "longitude": 116.3980, "latitude": 39.9080, "altitude": 120.0 },
      "velocity": { "east": -8.0, "north": 3.0, "up": 2.0 }
    }
  ]
}
```

### 4.3 获取无人机最新遥测数据

**接口描述**：获取指定无人机的最新遥测数据

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/telemetry/{droneId}/latest` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "droneId": "DRONE-001",
    "timestamp": "2024-01-15T10:30:00.123Z",
    "sequenceNumber": 12345,
    "position": {
      "longitude": 116.3972,
      "latitude": 39.9075,
      "altitude": 100.0
    },
    "velocity": {
      "east": 10.5,
      "north": 5.2,
      "up": 0.0
    },
    "batteryLevel": 85.5
  }
}
```

### 4.4 获取所有无人机最新状态

**接口描述**：获取所有无人机的最新状态快照

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/telemetry/snapshot` |
| 权限 | 只读 |

#### 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| status | string | 否 | 按状态筛选 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "timestamp": "2024-01-15T10:30:00Z",
    "count": 50,
    "drones": [
      {
        "id": "DRONE-001",
        "status": "Flying",
        "position": { "longitude": 116.3972, "latitude": 39.9075, "altitude": 100.0 },
        "velocity": { "east": 10.5, "north": 5.2, "up": 0.0 }
      }
    ]
  }
}
```

---

## 5. 轨迹查询 API

### 5.1 获取无人机历史轨迹

**接口描述**：获取指定无人机在指定时间范围内的历史轨迹

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/trajectory/{droneId}` |
| 权限 | 只读 |

#### 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| startTime | datetime | 是 | 开始时间 |
| endTime | datetime | 否 | 结束时间，默认当前时间 |
| maxPoints | int | 否 | 最大返回点数，默认 1000 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "droneId": "DRONE-001",
    "startTime": "2024-01-15T10:00:00Z",
    "endTime": "2024-01-15T10:30:00Z",
    "points": [
      {
        "timestamp": "2024-01-15T10:00:00Z",
        "position": { "longitude": 116.3972, "latitude": 39.9075, "altitude": 100.0 }
      },
      {
        "timestamp": "2024-01-15T10:00:01Z",
        "position": { "longitude": 116.3973, "latitude": 39.9075, "altitude": 100.0 }
      }
    ],
    "totalPoints": 1800
  }
}
```

### 5.2 预测无人机未来轨迹

**接口描述**：根据当前速度预测无人机未来一段时间的轨迹

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/trajectory/{droneId}/predict` |
| 权限 | 只读 |

#### 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| seconds | int | 否 | 预测时间（秒），默认 60 秒 |
| interval | double | 否 | 采样间隔（秒），默认 1 秒 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "droneId": "DRONE-001",
    "predictionHorizonSeconds": 60,
    "points": [
      {
        "timeOffset": 0,
        "position": { "longitude": 116.3972, "latitude": 39.9075, "altitude": 100.0 }
      },
      {
        "timeOffset": 30,
        "position": { "longitude": 116.3975, "latitude": 39.9076, "altitude": 100.0 }
      },
      {
        "timeOffset": 60,
        "position": { "longitude": 116.3978, "latitude": 39.9077, "altitude": 100.0 }
      }
    ]
  }
}
```

---

## 6. 冲突检测 API

### 6.1 获取当前活动冲突

**接口描述**：获取当前所有活动中的冲突告警

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/conflicts` |
| 权限 | 只读 |

#### 请求参数

| 参数名 | 类型 | 必填 | 说明 |
|-------|------|------|------|
| severity | string | 否 | 按严重程度筛选 |
| droneId | string | 否 | 按无人机 ID 筛选 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "totalCount": 3,
    "conflicts": [
      {
        "id": "conflict-001",
        "droneIds": ["DRONE-001", "DRONE-002"],
        "severity": "High",
        "tcpa": 15.5,
        "dcpa": 25.0,
        "predictedPosition": {
          "longitude": 116.3975,
          "latitude": 39.9078,
          "altitude": 100.0
        },
        "detectedTime": "2024-01-15T10:30:00Z"
      }
    ]
  }
}
```

### 6.2 获取指定无人机的冲突

**接口描述**：获取指定无人机涉及的所有冲突

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/conflicts/drone/{droneId}` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "droneId": "DRONE-001",
    "conflicts": [
      {
        "id": "conflict-001",
        "otherDroneId": "DRONE-002",
        "severity": "High",
        "tcpa": 15.5,
        "dcpa": 25.0
      }
    ]
  }
}
```

### 6.3 获取冲突统计

**接口描述**：获取冲突相关的统计数据

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/conflicts/stats` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "activeConflicts": 3,
    "criticalCount": 0,
    "highCount": 2,
    "mediumCount": 1,
    "lowCount": 0,
    "totalResolvedToday": 15,
    "detectionRatePerSecond": 2.5
  }
}
```

### 6.4 获取避让建议

**接口描述**：获取指定冲突的避让建议和决策

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/conflicts/{conflictId}/avoidance` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "conflictId": "conflict-001",
    "yieldingDroneId": "DRONE-001",
    "keepingDroneId": "DRONE-002",
    "reasoning": "DRONE-001 has lower priority",
    "recommendedActions": [
      {
        "actionType": "Climb",
        "description": "Increase altitude by 20 meters",
        "magnitude": 20.0,
        "unit": "meters"
      },
      {
        "actionType": "TurnRight",
        "description": "Turn right by 15 degrees",
        "magnitude": 15.0,
        "unit": "degrees"
      }
    ]
  }
}
```

---

## 7. 系统状态 API

### 7.1 健康检查

**接口描述**：系统健康检查端点

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/system/health` |
| 权限 | 公开 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "status": "Healthy",
    "timestamp": "2024-01-15T10:30:00Z",
    "version": "1.0.0",
    "services": {
      "telemetryService": "Healthy",
      "conflictDetectionService": "Healthy",
      "eventBus": "Healthy"
    }
  }
}
```

### 7.2 获取系统性能指标

**接口描述**：获取系统性能指标数据

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/system/metrics` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "timestamp": "2024-01-15T10:30:00Z",
    "telemetry": {
      "ingestionRate": 15000,
      "avgLatencyMs": 0.8,
      "p99LatencyMs": 2.5,
      "queueDepth": 500,
      "queueCapacity": 10000
    },
    "conflictDetection": {
      "detectionRate": 2.5,
      "avgDetectionTimeMs": 1.2,
      "dronesMonitored": 50
    }
  }
}
```

### 7.3 获取系统配置

**接口描述**：获取当前系统配置信息

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/system/config` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "safetyDistanceMeters": 30.0,
    "predictionHorizonSeconds": 60.0,
    "simulation": {
      "enabled": true,
      "droneCount": 50,
      "tickIntervalMs": 100
    },
    "telemetry": {
      "pipelineCapacity": 10000,
      "batchSize": 100
    }
  }
}
```

---

## 8. 模拟数据 API

### 8.1 启动模拟器

**接口描述**：启动无人机模拟器，生成模拟遥测数据

| 属性 | 值 |
|-----|----|
| HTTP 方法 | POST |
| 路径 | `/simulation/start` |
| 权限 | 可写 |

#### 请求体

```json
{
  "droneCount": 50,
  "baseLongitude": 116.3972,
  "baseLatitude": 39.9075,
  "speedMultiplier": 1.0
}
```

#### 响应示例

```json
{
  "code": 200,
  "message": "Simulation started",
  "data": {
    "droneCount": 50,
    "startedAt": "2024-01-15T10:30:00Z"
  }
}
```

### 8.2 停止模拟器

**接口描述**：停止无人机模拟器

| 属性 | 值 |
|-----|----|
| HTTP 方法 | POST |
| 路径 | `/simulation/stop` |
| 权限 | 可写 |

#### 响应示例

```json
{
  "code": 200,
  "message": "Simulation stopped"
}
```

### 8.3 获取模拟器状态

**接口描述**：获取当前模拟器的运行状态

| 属性 | 值 |
|-----|----|
| HTTP 方法 | GET |
| 路径 | `/simulation/status` |
| 权限 | 只读 |

#### 响应示例

```json
{
  "code": 200,
  "message": "success",
  "data": {
    "isRunning": true,
    "droneCount": 50,
    "startedAt": "2024-01-15T10:30:00Z",
    "totalTicks": 18000,
    "telemetryGenerated": 900000
  }
}
```

---

## 9. 数据接入协议

### 9.1 TCP 流式接入（高性能）

**推荐用于高吞吐量遥测数据接入**

#### 连接信息
- **协议**：TCP
- **端口**：5001
- **格式**：JSON Lines（每行一条 JSON）

#### 数据格式
```json
{"droneId":"DRONE-001","timestamp":"2024-01-15T10:30:00.123Z","position":{"longitude":116.3972,"latitude":39.9075,"altitude":100.0},"velocity":{"east":10.5,"north":5.2,"up":0.0}}
```

#### 性能特性
- 使用 System.IO.Pipelines 实现
- 单连接吞吐量：> 10万 消息/秒
- 平均处理延迟：< 1ms

### 9.2 WebSocket 实时推送

**推荐用于实时数据订阅**

#### 连接信息
- **URL**：`ws://localhost:5000/ws/telemetry`
- **协议**：WebSocket

#### 订阅消息
```json
{
  "action": "subscribe",
  "topic": "telemetry",
  "filters": {
    "droneIds": ["DRONE-001", "DRONE-002"]
  }
}
```

---

## 10. 版本历史

| 版本 | 日期 | 作者 | 变更说明 |
|-----|------|------|---------|
| 1.0 | 2024-01-15 | UTM Team | 初始版本 |
