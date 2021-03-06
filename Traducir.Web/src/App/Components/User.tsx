import axios from "axios";
import { autobind } from "core-decorators";
import React = require("react");
import { Link } from "react-router-dom";
import IConfig from "../../Models/Config";
import IUser from "../../Models/User";
import IUserInfo from "../../Models/UserInfo";
import { UserType, userTypeToString } from "../../Models/UserType";
import { NonUndefinedReactNode } from "../NonUndefinedReactNode";

interface IUserProps {
    user: IUser;
    currentUser?: IUserInfo;
    config: IConfig;
    refreshUsers: () => void;
    showErrorMessage: (messageOrCode: string | number) => void;
}

export default class User extends React.Component<IUserProps> {
    public render(): NonUndefinedReactNode {
        return <tr>
            <td>
                <a href={`https://${this.props.config.siteDomain}/users/${this.props.user.id}`} target="_blank">
                    {this.props.user.displayName} {this.props.user.isModerator ? "♦" : ""}
                </a>
            </td>
            <td>{userTypeToString(this.props.user.userType)}</td>
            <td>{this.props.currentUser && this.props.currentUser.canManageUsers &&
                <div className="btn-group" role="group">
                    <Link to={`/suggestions/${this.props.user.id}`} className="btn btn-sm btn-primary">view suggestions</Link>
                    {this.props.user.userType === UserType.User &&
                        <button type="button" className="btn btn-sm btn-warning" onClick={this.makeTrustedUser}>
                            Make trusted user
                    </button>
                    }
                    {this.props.user.userType === UserType.Banned &&
                        <button type="button" className="btn btn-sm btn-warning" onClick={this.makeRegularUser}>
                            Lift Ban
                    </button>
                    }
                    {this.props.user.userType === UserType.TrustedUser &&
                        <button type="button" className="btn btn-sm btn-danger" onClick={this.makeRegularUser}>
                            Make regular user
                    </button>
                    }
                    {this.props.user.userType !== UserType.Banned && this.props.user.userType !== UserType.TrustedUser && this.props.user.userType !== UserType.Reviewer && !this.props.user.isModerator &&
                        <button type="button" className="btn btn-sm btn-danger" onClick={this.banUser}>
                            Ban User
                    </button>
                    }
                </div>
            }</td>
        </tr>;
    }

    @autobind()
    private makeTrustedUser(): void {
        this.updateUserType(UserType.TrustedUser);
    }

    @autobind()
    private makeRegularUser(): void {
        this.updateUserType(UserType.User);
    }

    @autobind()
    private banUser(): void {
        this.updateUserType(UserType.Banned);
    }

    private async updateUserType(newType: UserType): Promise<void> {
        try {
            await axios.put("/app/api/users/change-type", {
                UserId: this.props.user.id,
                UserType: newType
            });
            this.props.refreshUsers();
        } catch (e) {
            if (e.response.status === 400) {
                this.props.showErrorMessage("Error updating user type");
            } else {
                this.props.showErrorMessage(e.response.status);
            }
        }
    }
}
