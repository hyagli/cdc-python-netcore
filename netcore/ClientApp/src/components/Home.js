import React, { Component } from 'react';

export class Home extends Component {
    static displayName = Home.name;

    render() {
        return (
            <div>
                <h1>Hello Protobuf</h1>
                <p>Welcome to the .net client example of protobuf CDC tryout</p>

                <p>To see the received data from Kafka in protobuf format click <a href="fetch-data">Fetch Data</a></p>
            </div>
        );
    }
}
